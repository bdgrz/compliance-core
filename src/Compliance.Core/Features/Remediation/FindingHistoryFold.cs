using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Read-side history for one finding. This may grow with its event stream; it is never retained
///     by <see cref="FindingAggregate"/> as command state.
/// </summary>
public sealed class FindingHistoryFold
{
    readonly HashSet<FindingAcceptanceIdentity> _acceptanceIdentities;

    public FindingHistoryFold(ulong streamVersion,
        IEnumerable<FindingAcceptanceView> acceptances,
        IEnumerable<FindingHistoryEntryView> history,
        IEnumerable<FindingClosureView> closures,
        IEnumerable<FindingHistorySourceLineage>? sourceLineage = null)
    {
        ArgumentNullException.ThrowIfNull(acceptances);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(closures);
        StreamVersion = streamVersion;
        var acceptanceArray = acceptances.ToArray();
        Acceptances = Array.AsReadOnly(acceptanceArray);
        History = Array.AsReadOnly(history.ToArray());
        Closures = Array.AsReadOnly(closures.ToArray());
        SourceLineage = Array.AsReadOnly((sourceLineage ?? []).ToArray());
        _acceptanceIdentities = acceptanceArray.Select(static acceptance =>
            new FindingAcceptanceIdentity(acceptance.Kind, acceptance.RecordId,
                acceptance.DecisionId)).ToHashSet();
    }

    public ulong StreamVersion { get; }
    public IReadOnlyList<FindingAcceptanceView> Acceptances { get; }
    public IReadOnlyList<FindingHistoryEntryView> History { get; }
    public IReadOnlyList<FindingClosureView> Closures { get; }
    public IReadOnlyList<FindingHistorySourceLineage> SourceLineage { get; }

    public bool HasAcceptance(FindingAcceptanceIdentity identity) =>
        _acceptanceIdentities.Contains(identity);

    public static async ValueTask<FindingHistoryFold> ReadAsync(IDomainEventReader events,
        Uuid tenantId, Uuid programId, Uuid findingId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(events);
        var stream = new FindingAggregate(tenantId, programId, findingId).Stream;
        var streamId = FindingAggregate.StreamId(programId, findingId);
        var history = new List<FindingHistoryEntryView>();
        var acceptances = new List<FindingAcceptanceView>();
        var closures = new List<FindingClosureView>();
        var sourceLineage = new List<FindingHistorySourceLineage>();
        var importedEventIds = new HashSet<Uuid>();
        ulong expectedVersion = 1;
        long revision = 0;
        ulong? lastSourceProgramOffset = null;
        ulong lastSourceProgramVersion = 0;
        DateTimeOffset? priorOccurredAt = null;
        FindingHistoryEventImported? pendingImport = null;

        await foreach (var record in events.ReadAsync(stream, 0, ct).ConfigureAwait(false))
        {
            if (record.Stream != stream || record.Event?.Metadata is not { } metadata ||
                metadata.AggregateId != streamId || metadata.EventId == Uuid.Empty ||
                metadata.AggregateVersion != expectedVersion || metadata.OccurredOn == default ||
                priorOccurredAt is { } prior && metadata.OccurredOn < prior)
                throw new InvalidOperationException("The finding stream history is invalid.");

            if (record.Event is FindingHistoryEventImported imported)
            {
                if (pendingImport is not null || imported.TenantId != tenantId ||
                    imported.ProgramId != programId || imported.FindingId != findingId ||
                    imported.Revision != revision + 1 || imported.SourceEventId == Uuid.Empty ||
                    imported.SourceProgramStreamOffset == ulong.MaxValue ||
                    lastSourceProgramOffset is { } priorSourceOffset &&
                    imported.SourceProgramStreamOffset <= priorSourceOffset ||
                    imported.SourceProgramStreamVersion == 0 ||
                    imported.SourceProgramStreamVersion <= lastSourceProgramVersion ||
                    imported.SourceOccurredAt == default ||
                    imported.SourceOccurredAt.Offset != TimeSpan.Zero ||
                    !FindingHistoryEventPayloads.IsHash(imported.SourceEventSha256) ||
                    !importedEventIds.Add(imported.SourceEventId))
                    throw new InvalidOperationException("The imported finding lineage is invalid.");
                pendingImport = imported;
                priorOccurredAt = metadata.OccurredOn;
                expectedVersion++;
                continue;
            }

            if (pendingImport is { } marker)
            {
                if (!FindingHistoryEventPayloads.TryCreate(record.Event, tenantId, programId,
                        findingId, out var payload) || payload.Revision != marker.Revision ||
                    payload.Kind != marker.SourceEventKind ||
                    FindingHistoryEventPayloads.Hash(payload.Json) != marker.SourceEventSha256)
                    throw new InvalidOperationException(
                        "The finding-import marker is not followed by its matching source fact.");

                lastSourceProgramOffset = marker.SourceProgramStreamOffset;
                lastSourceProgramVersion = marker.SourceProgramStreamVersion;
                sourceLineage.Add(new FindingHistorySourceLineage(marker.SourceEventId,
                    marker.SourceProgramStreamOffset, marker.SourceProgramStreamVersion,
                    marker.SourceEventKind,
                    marker.SourceOccurredAt, expectedVersion));
                pendingImport = null;
            }

            if (!TryReadEntry(record.Event,
                    tenantId, programId, findingId, out var nextRevision,
                    out var entry, out var acceptance, out var closure) ||
                nextRevision != revision + 1)
                throw new InvalidOperationException("The finding stream history is invalid.");

            history.Add(entry);
            if (acceptance is not null)
                acceptances.Add(acceptance);
            if (closure is not null)
                closures.Add(closure);
            revision = nextRevision;
            priorOccurredAt = metadata.OccurredOn;
            expectedVersion++;
        }

        if (pendingImport is not null)
            throw new InvalidOperationException(
                "The finding stream ends with an unpaired legacy-import marker.");

        return new FindingHistoryFold(expectedVersion - 1, acceptances, history, closures,
            sourceLineage);
    }

    static bool TryReadEntry(DomainEvent domainEvent, Uuid tenantId, Uuid programId,
        Uuid findingId, out long revision, out FindingHistoryEntryView entry,
        out FindingAcceptanceView? acceptance, out FindingClosureView? closure)
    {
        revision = 0;
        entry = null!;
        acceptance = null;
        closure = null;
        switch (domainEvent)
        {
            case FindingRaised ev when InScope(ev.TenantId, ev.ProgramId, ev.FindingId,
                    tenantId, programId, findingId) && ev.Source is not null:
                revision = ev.Revision;
                entry = new FindingHistoryEntryView(revision, "raised", ev.Title, ev.RaisedBy,
                    ev.RaisedAt);
                return revision > 0 && ev.RaisedAt != default;
            case FindingRevised ev when InScope(ev.TenantId, ev.ProgramId, ev.FindingId,
                    tenantId, programId, findingId):
                revision = ev.Revision;
                entry = new FindingHistoryEntryView(revision, "revised",
                    $"Severity {ev.Severity}, owner {ev.OwnerMemberId}, due {Date(ev.DueOn)}: {ev.Reason}",
                    ev.RevisedBy, ev.RevisedAt);
                return revision > 0 && ev.RevisedAt != default;
            case CorrectiveActionAdded ev when InScope(ev.TenantId, ev.ProgramId, ev.FindingId,
                    tenantId, programId, findingId) && ev.Action is not null:
                revision = ev.Revision;
                entry = new FindingHistoryEntryView(revision, "corrective_action_added",
                    ev.Action.Description, ev.Action.AddedBy, ev.Action.AddedAt);
                return revision > 0 && ev.Action.AddedAt != default;
            case CorrectiveActionCompleted ev when InScope(ev.TenantId, ev.ProgramId, ev.FindingId,
                    tenantId, programId, findingId):
                revision = ev.Revision;
                entry = new FindingHistoryEntryView(revision, "corrective_action_completed",
                    ev.ResolutionNotes, ev.CompletedBy, ev.CompletedAt);
                return revision > 0 && ev.CompletedAt != default;
            case FindingAcceptanceLinked ev when InScope(ev.TenantId, ev.ProgramId, ev.FindingId,
                    tenantId, programId, findingId) && ev.Acceptance is not null:
                revision = ev.Revision;
                acceptance = ev.Acceptance;
                entry = new FindingHistoryEntryView(revision, "acceptance_linked",
                    $"{ev.Acceptance.Kind} {ev.Acceptance.RecordId} until {ev.Acceptance.ExpiresAt:O}",
                    ev.Acceptance.LinkedBy, ev.Acceptance.LinkedAt);
                return revision > 0 && ev.Acceptance.LinkedAt != default;
            case FindingClosed ev when InScope(ev.TenantId, ev.ProgramId, ev.FindingId,
                    tenantId, programId, findingId) && ev.Closure is not null:
                revision = ev.Revision;
                closure = ev.Closure;
                entry = new FindingHistoryEntryView(revision, "closed",
                    $"Decision {ev.Closure.DecisionId}: {ev.Closure.Rationale}",
                    ev.Closure.ClosedBy, ev.Closure.ClosedAt);
                return revision > 0 && ev.Closure.ClosedAt != default;
            case FindingReopened ev when InScope(ev.TenantId, ev.ProgramId, ev.FindingId,
                    tenantId, programId, findingId):
                revision = ev.Revision;
                entry = new FindingHistoryEntryView(revision, "reopened", ev.Reason,
                    ev.ReopenedBy, ev.ReopenedAt);
                return revision > 0 && ev.ReopenedAt != default;
            default:
                return false;
        }
    }

    static bool InScope(Uuid eventTenantId, Uuid eventProgramId, Uuid eventFindingId,
        Uuid tenantId, Uuid programId, Uuid findingId) => eventTenantId == tenantId &&
        eventProgramId == programId && eventFindingId == findingId;

    static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

public sealed record FindingHistorySourceLineage(Uuid SourceEventId,
    ulong SourceProgramStreamOffset, ulong SourceProgramStreamVersion, string SourceEventKind,
    DateTimeOffset SourceOccurredAt, ulong TargetStreamVersion);
