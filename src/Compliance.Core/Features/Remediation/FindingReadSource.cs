using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Reads the recorded owner generation, preserving legacy availability until migration completes.</summary>
public sealed class FindingReadSource(IAggregateReader reader, IDomainEventReader events)
{
    const int MaximumSourceRecords = 100_000;

    public async ValueTask<Result<FindingReadSnapshot>> ReadAsync(Uuid tenantId, Uuid programId,
        DateTimeOffset now, CancellationToken ct)
    {
        var migration = await reader.HydrateAsync(new FindingMigrationAggregate(tenantId, programId), ct)
            .ConfigureAwait(false);
        if (!migration.IsComplete)
        {
            var legacy = await reader.HydrateAsync(new RemediationLedger(tenantId, programId), ct).ConfigureAwait(false);
            var findings = legacy.ReadAll(now);
            var confirmed = await reader.HydrateAsync(new FindingMigrationAggregate(tenantId, programId), ct)
                .ConfigureAwait(false);
            var currentLegacy = await reader.HydrateAsync(new RemediationMigrationFenceAggregate(tenantId, programId), ct)
                .ConfigureAwait(false);
            if (confirmed.Version != migration.Version || currentLegacy.Version != legacy.Version)
                return Changed();
            return Result<FindingReadSnapshot>.Success(new(findings,
                findings.ToDictionary(static finding => finding.FindingId,
                    finding => legacy.ReadClosures(finding.FindingId)), false));
        }

        var before = await ReadTargetRecordsAsync(tenantId, programId, ct).ConfigureAwait(false);
        if (!before.IsSuccess)
            return Result<FindingReadSnapshot>.Failure(before.Error);
        var ids = new HashSet<Uuid>();
        foreach (var record in before.Value)
        {
            if (!TryIdentity(record.Event, out var sourceTenant, out var sourceProgram, out var findingId) ||
                sourceTenant != tenantId || record.Stream != new FindingAggregate(sourceTenant,
                    sourceProgram, findingId).Stream || record.Event.Metadata.AggregateId !=
                    FindingAggregate.StreamId(sourceProgram, findingId))
                return InvalidSource();
            if (sourceProgram == programId)
                ids.Add(findingId);
        }
        var items = new List<FindingView>();
        var closures = new Dictionary<Uuid, IReadOnlyList<FindingClosureView>>();
        foreach (var findingId in ids.OrderBy(static id => id.ToString(), StringComparer.Ordinal))
        {
            var finding = await reader.HydrateAsync(new FindingAggregate(tenantId, programId, findingId), ct)
                .ConfigureAwait(false);
            var history = await FindingHistoryFold.ReadAsync(events, tenantId, programId, findingId, ct)
                .ConfigureAwait(false);
            if (finding.Read(history, now) is not { } view)
                return Changed();
            items.Add(view);
            closures.Add(findingId, history.Closures);
        }
        var after = await ReadTargetRecordsAsync(tenantId, programId, ct).ConfigureAwait(false);
        var finalMigration = await reader.HydrateAsync(new FindingMigrationAggregate(tenantId, programId), ct)
            .ConfigureAwait(false);
        if (!after.IsSuccess)
            return Result<FindingReadSnapshot>.Failure(after.Error);
        if (finalMigration.Version != migration.Version || !SameRecords(before.Value, after.Value))
            return Changed();
        return Result<FindingReadSnapshot>.Success(new(Array.AsReadOnly(items
                .OrderBy(static finding => finding.RaisedAt)
                .ThenBy(static finding => finding.FindingId.ToString(), StringComparer.Ordinal).ToArray()), closures, true,
            Array.AsReadOnly(before.Value.Select(static record => record.Record).ToArray())));
    }

    public async ValueTask<Result<FindingView>> ReadOneAsync(Uuid tenantId, Uuid programId,
        Uuid findingId, DateTimeOffset now, CancellationToken ct)
    {
        var migration = await reader.HydrateAsync(new FindingMigrationAggregate(tenantId, programId), ct)
            .ConfigureAwait(false);
        if (!migration.IsComplete)
        {
            var legacy = await ReadAsync(tenantId, programId, now, ct).ConfigureAwait(false);
            return !legacy.IsSuccess ? Result<FindingView>.Failure(legacy.Error)
                : legacy.Value.Read(findingId) is { } legacyFinding ? Result<FindingView>.Success(legacyFinding)
                : Result<FindingView>.Failure(new RequestError(RequestErrorKind.NotFound, "The finding was not found."));
        }
        var finding = await reader.HydrateAsync(new FindingAggregate(tenantId, programId, findingId), ct)
            .ConfigureAwait(false);
        var history = await FindingHistoryFold.ReadAsync(events, tenantId, programId, findingId, ct)
            .ConfigureAwait(false);
        var confirmed = await reader.HydrateAsync(new FindingAggregate(tenantId, programId, findingId), ct)
            .ConfigureAwait(false);
        if (confirmed.Version != finding.Version || history.StreamVersion != finding.Version)
            return Result<FindingView>.Failure(Changed().Error!);
        return finding.Read(history, now) is { } view ? Result<FindingView>.Success(view)
            : Result<FindingView>.Failure(new RequestError(RequestErrorKind.NotFound, "The finding was not found."));
    }

    async ValueTask<Result<IReadOnlyList<CapturedFindingRecord>>> ReadTargetRecordsAsync(Uuid tenantId, Uuid programId,
        CancellationToken ct)
    {
        var records = new List<CapturedFindingRecord>();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(), FindingAggregate.Area),
                           EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            if (!TryIdentity(record.Event, out var sourceTenant, out var sourceProgram, out var findingId) ||
                sourceTenant != tenantId)
                return InvalidRecords();
            if (sourceProgram != programId)
                continue;
            if (record.Stream != new FindingAggregate(tenantId, programId, findingId).Stream)
                return InvalidRecords();
            if (records.Count >= MaximumSourceRecords)
                return Result<IReadOnlyList<CapturedFindingRecord>>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The finding source inventory exceeds its bounded read limit."));
            var payloadHash = PayloadHash(record.Event);
            if (payloadHash is null)
                return InvalidRecords();
            records.Add(new(record, payloadHash));
        }
        return Result<IReadOnlyList<CapturedFindingRecord>>.Success(Array.AsReadOnly(records
            .OrderBy(static record => record.Stream.Resource, StringComparer.Ordinal)
            .ThenBy(static record => record.Event.Metadata.AggregateVersion).ToArray()));
    }

    static bool TryIdentity(DomainEvent ev, out Uuid tenantId, out Uuid programId, out Uuid findingId)
    {
        ev = FindingEventFacts.Business(ev);
        (tenantId, programId, findingId) = ev switch
        {
            FindingRaised fact => (fact.TenantId, fact.ProgramId, fact.FindingId),
            FindingRevised fact => (fact.TenantId, fact.ProgramId, fact.FindingId),
            CorrectiveActionAdded fact => (fact.TenantId, fact.ProgramId, fact.FindingId),
            CorrectiveActionCompleted fact => (fact.TenantId, fact.ProgramId, fact.FindingId),
            FindingAcceptanceLinked fact => (fact.TenantId, fact.ProgramId, fact.FindingId),
            FindingClosed fact => (fact.TenantId, fact.ProgramId, fact.FindingId),
            FindingReopened fact => (fact.TenantId, fact.ProgramId, fact.FindingId),
            FindingHistoryEventImported fact => (fact.TenantId, fact.ProgramId, fact.FindingId),
            _ => (Uuid.Empty, Uuid.Empty, Uuid.Empty),
        };
        return tenantId != Uuid.Empty && programId != Uuid.Empty && findingId != Uuid.Empty;
    }

    internal static string? PayloadHash(DomainEvent ev)
    {
        if (FindingEventFacts.RequestId(ev) is not null)
            return FindingEventFacts.Hash(ev);
        if (ev is FindingHistoryEventImported marker)
            return FindingHistoryEventPayloads.Hash(JsonSerializer.SerializeToElement(marker,
                ComplianceCoreJsonContext.Default.FindingHistoryEventImported));
        if (ev is RemediationPartitioningStarted fence)
            return FindingHistoryEventPayloads.Hash(JsonSerializer.SerializeToElement(fence,
                ComplianceCoreJsonContext.Default.RemediationPartitioningStarted));
        return TryIdentity(ev, out var tenantId, out var programId, out var findingId) &&
               FindingHistoryEventPayloads.TryCreate(ev, tenantId, programId, findingId, out var payload)
            ? FindingHistoryEventPayloads.Hash(payload.Json) : null;
    }

    static bool SameRecords(IReadOnlyList<CapturedFindingRecord> left, IReadOnlyList<CapturedFindingRecord> right) =>
        left.Count == right.Count && left.Zip(right).All(static pair =>
            pair.First.Stream == pair.Second.Stream && pair.First.Record.ResourceOffset == pair.Second.Record.ResourceOffset &&
            pair.First.Event.Metadata == pair.Second.Event.Metadata &&
            pair.First.PayloadSha256 == pair.Second.PayloadSha256);

    static Result<FindingReadSnapshot> Changed() => Result<FindingReadSnapshot>.Failure(new RequestError(
        RequestErrorKind.Conflict, "Finding ownership or source history changed during the read. Retry.", isTransient: true));
    static Result<FindingReadSnapshot> InvalidSource() => Result<FindingReadSnapshot>.Failure(new RequestError(
        RequestErrorKind.Conflict, "The finding source inventory is invalid."));
    static Result<IReadOnlyList<CapturedFindingRecord>> InvalidRecords() =>
        Result<IReadOnlyList<CapturedFindingRecord>>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The finding source inventory is invalid."));
    sealed record CapturedFindingRecord(DomainEventRecord Record, string PayloadSha256)
    {
        public EventStreamAddress Stream => Record.Stream;
        public DomainEvent Event => Record.Event;
        public EventCursor NextCursor => Record.NextCursor;
    }
}
