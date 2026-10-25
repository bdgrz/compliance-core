using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Snapshots;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>
///     Captures each program finding and its exact remediation history at the Type I cutoff from the finding owner and
///     the owner of each admitted corrective action. A fact belongs to the baseline only when both its business
///     decision time and its envelope time are inside the cutoff, and the finding facts must form a complete prefix.
/// </summary>
public sealed class FindingTypeIBaselineSourceContributor(
    IDomainEventReader events, IAggregateReader aggregates) : ITypeIBaselineSourceContributor
{
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

        var through = context.RecordedThrough ?? new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue,
            DateTimeKind.Utc));
        var ids = new SortedSet<Uuid>(Comparer<Uuid>.Create(static (left, right) =>
            string.CompareOrdinal(left.ToString(), right.ToString())));
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(engagement.TenantId.ToString(),
                           FindingAggregate.Area), EventCursor.Start, ct).ConfigureAwait(false))
            if (!record.Event.Metadata.IsAudit && record.Event is FindingRaisedV2 raised &&
                raised.TenantId == engagement.TenantId && raised.ProgramId == engagement.ProgramId)
                ids.Add(raised.FindingId);

        var sources = new List<TypeIBaselineSourceReference>();
        foreach (var findingId in ids)
        {
            var finding = await aggregates.HydrateAsync(new FindingAggregate(engagement.TenantId, engagement.ProgramId,
                findingId), ct).ConfigureAwait(false);
            var history = new List<FindingTypeIBaselineSourceEvent>();
            long asOfRevision = 0;
            var complete = true;
            foreach (var fact in finding.Facts)
            {
                if (!(context.RecordedThrough is null ? InsideCutoff(fact, through) :
                    await context.IncludesAsync(finding.Stream, fact, ct).ConfigureAwait(false)))
                {
                    complete = false;
                    continue;
                }

                if (!complete)
                    return InvalidHistory("The Type I finding history is not a complete prefix.");
                history.Add(Capture(fact));
                asOfRevision = RevisionOf(fact);
            }

            if (asOfRevision == 0)
                continue;
            foreach (var admitted in finding.Admitted)
            {
                var action = await aggregates.HydrateAsync(new CorrectiveAction(engagement.TenantId,
                    engagement.ProgramId, findingId, admitted.Operation.LogicalId), ct).ConfigureAwait(false);
                // The action facts are part of the finding's remediation history from the admission on.
                if (!(context.RecordedThrough is null ? InsideCutoff(admitted, through) :
                    await context.IncludesAsync(finding.Stream, admitted, ct).ConfigureAwait(false)))
                    continue;
                foreach (var fact in ActionFacts(action))
                    if (context.RecordedThrough is null ? InsideCutoff(fact, through) :
                        await context.IncludesAsync(action.Stream, fact, ct).ConfigureAwait(false))
                        history.Add(Capture(fact));
            }

            try
            {
                var content = new FindingTypeIBaselineSourceContent(engagement.PeriodStart, engagement.TenantId,
                    engagement.ProgramId, findingId, asOfRevision, Array.AsReadOnly(history.ToArray()));
                sources.Add(new TypeIBaselineSourceReference("finding", findingId,
                    Uuid.CreateVersion5(findingId, $"finding_v1:{asOfRevision}"), asOfRevision, Digest(content)));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or
                InvalidOperationException or NotSupportedException)
            {
                return InvalidHistory("A finding fact could not be captured.");
            }
        }

        return Result<TypeIBaselineSourceContribution>.Success(
            new TypeIBaselineSourceContribution(Family, sources.AsReadOnly(), [], []));
    }

    static IEnumerable<DomainEvent> ActionFacts(CorrectiveAction action)
    {
        if (action.Added is { } added)
            yield return added;
        if (action.Completed is { } completed)
            yield return completed;
    }

    static bool InsideCutoff(DomainEvent fact, DateTimeOffset through) =>
        fact is INativeOperationFact native && native.Operation.AcceptedAt <= through &&
        fact.Metadata.OccurredOn <= through;

    static long RevisionOf(DomainEvent fact) => fact switch
    {
        FindingRaisedV2 ev => ev.Revision,
        FindingRevisedV2 ev => ev.Revision,
        CorrectiveActionAdmitted ev => ev.Revision,
        FindingAcceptanceLinkedV2 ev => ev.Revision,
        FindingClosureAccepted ev => ev.Revision,
        FindingClosedV2 ev => ev.Revision,
        FindingClosureCanceled ev => ev.Revision,
        FindingReopenedV2 ev => ev.Revision,
        CorrectiveActionAddedV2 ev => ev.Revision,
        CorrectiveActionCompletedV2 ev => ev.Revision,
        _ => throw new InvalidOperationException($"{fact.GetType().Name} is not a remediation fact."),
    };

    static FindingTypeIBaselineSourceEvent Capture(DomainEvent fact)
    {
        var metadata = fact.Metadata;
        var info = ComplianceCoreJsonContext.Default.GetTypeInfo(fact.GetType())
            ?? throw new JsonException("The remediation fact has no registered serializer.");
        return new FindingTypeIBaselineSourceEvent(metadata.EventId, metadata.AggregateVersion, RevisionOf(fact),
            metadata.OccurredOn, KindOf(fact), JsonSerializer.SerializeToElement(fact, info));
    }

    static string KindOf(DomainEvent fact) => fact switch
    {
        FindingRaisedV2 => "finding_raised",
        FindingRevisedV2 => "finding_revised",
        CorrectiveActionAdmitted => "corrective_action_admitted",
        FindingAcceptanceLinkedV2 => "finding_acceptance_linked",
        FindingClosureAccepted => "finding_closure_accepted",
        FindingClosedV2 => "finding_closed",
        FindingClosureCanceled => "finding_closure_canceled",
        FindingReopenedV2 => "finding_reopened",
        CorrectiveActionAddedV2 => "corrective_action_added",
        CorrectiveActionCompletedV2 => "corrective_action_completed",
        _ => throw new JsonException("The remediation stream contains an unknown event."),
    };

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
        return Convert.ToHexStringLower(SnapshotSourceCapture.HashData(material));
    }

    static Result<TypeIBaselineSourceContribution> InvalidHistory(string message) =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
