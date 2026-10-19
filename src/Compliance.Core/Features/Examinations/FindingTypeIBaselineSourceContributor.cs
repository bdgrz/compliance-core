using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Captures each program finding and its exact remediation history at the Type I cutoff.</summary>
public sealed class FindingTypeIBaselineSourceContributor(
    IDomainEventReader events, IAggregateReader aggregates) : ITypeIBaselineSourceContributor
{
    const int MaximumEvents = 100_000;
    const string DigestDomain = "bdgrz.snapshot.source.finding.v1\0";

    public string Family => "findings";

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct) =>
        ReadAsync(engagement, new TypeIBaselineSourceReadContext(Uuid.Empty, []), ct);

    public async ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context,
        CancellationToken ct)
    {
        if (engagement is null || context?.PreviouslyCapturedSources is null ||
            engagement.TenantId == Uuid.Empty || engagement.ProgramId == Uuid.Empty ||
            engagement.PeriodStart == default)
            return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "A finding baseline read requires its engagement and source context."));

        var stream = new EventStreamAddress(engagement.TenantId.ToString(), "remediation",
            engagement.ProgramId.ToString());
        var historyRead = await ReadStreamTwiceAsync(stream, ct).ConfigureAwait(false);
        if (!historyRead.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(historyRead.Error);

        var through = new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue,
            DateTimeKind.Utc));
        var histories = new Dictionary<Uuid, FindingHistory>();
        var eventIds = new HashSet<Uuid>();
        ulong expectedStreamVersion = 1;
        DateTimeOffset? priorOccurredAt = null;
        foreach (var record in historyRead.Value)
        {
            if (record.Stream != stream || record.Event?.Metadata is not { } metadata ||
                metadata.AggregateId != engagement.ProgramId || metadata.EventId == Uuid.Empty ||
                !eventIds.Add(metadata.EventId) || metadata.AggregateVersion != expectedStreamVersion ||
                metadata.OccurredOn == default || priorOccurredAt is { } prior &&
                metadata.OccurredOn < prior || !TryReadFindingEvent(record.Event,
                    engagement.TenantId, engagement.ProgramId, out var findingId,
                    out var findingRevision, out var occurredAt))
                return InvalidHistory();

            if (!histories.TryGetValue(findingId, out var history))
            {
                history = new FindingHistory(findingId);
                histories.Add(findingId, history);
            }
            var isRaise = record.Event is FindingRaised;
            if (findingRevision != history.CurrentRevision + 1 ||
                (history.CurrentRevision == 0) != isRaise)
                return InvalidHistory();
            if (occurredAt != metadata.OccurredOn)
                return InvalidHistory();

            history.CurrentRevision = findingRevision;
            if (occurredAt <= through)
            {
                try
                {
                    history.AsOfEvents.Add(Capture(record.Event, metadata, findingRevision));
                }
                catch (Exception exception) when (exception is ArgumentException or JsonException or
                    InvalidOperationException or NotSupportedException)
                {
                    return InvalidHistory();
                }
                history.AsOfRevision = findingRevision;
            }
            expectedStreamVersion++;
            priorOccurredAt = metadata.OccurredOn;
        }

        try
        {
            var ledger = await aggregates.HydrateAsync(new RemediationLedger(engagement.TenantId,
                engagement.ProgramId), ct).ConfigureAwait(false);
            var currentViews = ledger.ReadAll(through);
            if (ledger.CommittedStreamPosition != (ulong)historyRead.Value.Count ||
                currentViews.Count != histories.Count || currentViews.Any(view =>
                    view.TenantId != engagement.TenantId || view.ProgramId != engagement.ProgramId ||
                    !histories.TryGetValue(view.FindingId, out var history) ||
                    view.Revision != history.CurrentRevision))
                return InvalidHistory();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return InvalidHistory();
        }

        var sources = new List<TypeIBaselineSourceReference>();
        foreach (var history in histories.Values.Where(static history => history.AsOfRevision > 0)
                     .OrderBy(static history => history.FindingId.ToString(), StringComparer.Ordinal))
        {
            try
            {
                var content = new FindingTypeIBaselineSourceContent(engagement.PeriodStart,
                    engagement.TenantId, engagement.ProgramId, history.FindingId,
                    history.AsOfRevision, Array.AsReadOnly(history.AsOfEvents.ToArray()));
                sources.Add(new TypeIBaselineSourceReference("finding", history.FindingId,
                    Uuid.CreateVersion5(history.FindingId, $"finding_v1:{history.AsOfRevision}"),
                    history.AsOfRevision, Digest(content)));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or
                InvalidOperationException or NotSupportedException)
            {
                return InvalidHistory();
            }
        }

        return Result<TypeIBaselineSourceContribution>.Success(
            new TypeIBaselineSourceContribution(Family, sources.AsReadOnly(), [], []));
    }

    static bool TryReadFindingEvent(DomainEvent domainEvent, Uuid tenantId, Uuid programId,
        out Uuid findingId, out long revision, out DateTimeOffset occurredAt)
    {
        findingId = Uuid.Empty;
        revision = 0;
        occurredAt = default;
        switch (domainEvent)
        {
            case FindingRaised ev when ev.TenantId == tenantId && ev.ProgramId == programId &&
                ev.FindingId != Uuid.Empty && ev.Source is not null && ev.Links is not null:
                findingId = ev.FindingId;
                revision = ev.Revision;
                occurredAt = ev.RaisedAt;
                return revision > 0 && occurredAt != default;
            case FindingRevised ev when ev.TenantId == tenantId && ev.ProgramId == programId:
                findingId = ev.FindingId;
                revision = ev.Revision;
                occurredAt = ev.RevisedAt;
                return findingId != Uuid.Empty && revision > 0 && occurredAt != default;
            case CorrectiveActionAdded ev when ev.TenantId == tenantId && ev.ProgramId == programId:
                findingId = ev.FindingId;
                revision = ev.Revision;
                occurredAt = ev.Action?.AddedAt ?? default;
                return findingId != Uuid.Empty && revision > 0 && occurredAt != default;
            case CorrectiveActionCompleted ev when ev.TenantId == tenantId && ev.ProgramId == programId:
                findingId = ev.FindingId;
                revision = ev.Revision;
                occurredAt = ev.CompletedAt;
                return findingId != Uuid.Empty && revision > 0 && occurredAt != default;
            case FindingAcceptanceLinked ev when ev.TenantId == tenantId && ev.ProgramId == programId:
                findingId = ev.FindingId;
                revision = ev.Revision;
                occurredAt = ev.Acceptance?.LinkedAt ?? default;
                return findingId != Uuid.Empty && revision > 0 && occurredAt != default;
            case FindingClosed ev when ev.TenantId == tenantId && ev.ProgramId == programId:
                findingId = ev.FindingId;
                revision = ev.Revision;
                occurredAt = ev.Closure?.ClosedAt ?? default;
                return findingId != Uuid.Empty && revision > 0 && occurredAt != default;
            case FindingReopened ev when ev.TenantId == tenantId && ev.ProgramId == programId:
                findingId = ev.FindingId;
                revision = ev.Revision;
                occurredAt = ev.ReopenedAt;
                return findingId != Uuid.Empty && revision > 0 && occurredAt != default;
            default:
                return false;
        }
    }

    static FindingTypeIBaselineSourceEvent Capture(DomainEvent domainEvent,
        DomainEventMetadata metadata, long findingRevision)
    {
        var context = ComplianceCoreJsonContext.Default;
        var (kind, payload) = domainEvent switch
        {
            FindingRaised ev => ("finding_raised",
                JsonSerializer.SerializeToElement(ev, context.FindingRaised)),
            FindingRevised ev => ("finding_revised",
                JsonSerializer.SerializeToElement(ev, context.FindingRevised)),
            CorrectiveActionAdded ev => ("corrective_action_added",
                JsonSerializer.SerializeToElement(ev, context.CorrectiveActionAdded)),
            CorrectiveActionCompleted ev => ("corrective_action_completed",
                JsonSerializer.SerializeToElement(ev, context.CorrectiveActionCompleted)),
            FindingAcceptanceLinked ev => ("finding_acceptance_linked",
                JsonSerializer.SerializeToElement(ev, context.FindingAcceptanceLinked)),
            FindingClosed ev => ("finding_closed",
                JsonSerializer.SerializeToElement(ev, context.FindingClosed)),
            FindingReopened ev => ("finding_reopened",
                JsonSerializer.SerializeToElement(ev, context.FindingReopened)),
            _ => throw new JsonException("The remediation stream contains an unknown event."),
        };
        return new FindingTypeIBaselineSourceEvent(metadata.EventId,
            metadata.AggregateVersion, findingRevision, metadata.OccurredOn, kind, payload);
    }

    static string Digest(FindingTypeIBaselineSourceContent content)
    {
        var element = JsonSerializer.SerializeToElement(content,
            ComplianceCoreJsonContext.Default.FindingTypeIBaselineSourceContent);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream,
                   SnapshotContentIdentity.CanonicalWriterOptions))
            SnapshotContentIdentity.WriteCanonical(writer, element);
        var domain = Encoding.UTF8.GetBytes(DigestDomain);
        var bytes = stream.ToArray();
        var material = new byte[domain.Length + bytes.Length];
        domain.CopyTo(material, 0);
        bytes.CopyTo(material, domain.Length);
        return Convert.ToHexStringLower(SHA256.HashData(material));
    }

    async ValueTask<Result<IReadOnlyList<ReadRecord>>> ReadStreamTwiceAsync(
        EventStreamAddress stream, CancellationToken ct)
    {
        var first = await ReadStreamAsync(stream, ct).ConfigureAwait(false);
        if (!first.IsSuccess)
            return first;
        var second = await ReadStreamAsync(stream, ct).ConfigureAwait(false);
        if (!second.IsSuccess)
            return second;
        return SameHistory(first.Value, second.Value)
            ? first
            : Result<IReadOnlyList<ReadRecord>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The remediation stream changed during the Type I source read.", isTransient: true));
    }

    async ValueTask<Result<IReadOnlyList<ReadRecord>>> ReadStreamAsync(
        EventStreamAddress stream, CancellationToken ct)
    {
        var records = new List<ReadRecord>();
        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (records.Count >= MaximumEvents)
                return InvalidHistory<IReadOnlyList<ReadRecord>>();
            records.Add(new ReadRecord(record.Stream, record.Event, record.NextCursor));
        }
        return Result<IReadOnlyList<ReadRecord>>.Success(records.AsReadOnly());
    }

    static bool SameHistory(IReadOnlyList<ReadRecord> left, IReadOnlyList<ReadRecord> right) =>
        left.Count == right.Count && left.Zip(right).All(pair =>
            pair.First.Stream == pair.Second.Stream && pair.First.NextCursor == pair.Second.NextCursor &&
            pair.First.Event?.GetType() == pair.Second.Event?.GetType() &&
            pair.First.Event?.Metadata == pair.Second.Event?.Metadata &&
            Equals(pair.First.Event, pair.Second.Event));

    static Result<TypeIBaselineSourceContribution> InvalidHistory() =>
        InvalidHistory<TypeIBaselineSourceContribution>();

    static Result<T> InvalidHistory<T>() => Result<T>.Failure(new RequestError(
        RequestErrorKind.Conflict, "The remediation source stream is invalid or inconsistent."));

    sealed class FindingHistory(Uuid findingId)
    {
        public Uuid FindingId { get; } = findingId;
        public long CurrentRevision { get; set; }
        public long AsOfRevision { get; set; }
        public List<FindingTypeIBaselineSourceEvent> AsOfEvents { get; } = [];
    }

    sealed record ReadRecord(EventStreamAddress Stream, DomainEvent? Event, EventCursor NextCursor);
}
