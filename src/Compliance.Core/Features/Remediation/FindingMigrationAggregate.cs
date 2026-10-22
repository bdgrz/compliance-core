using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Bounded per-program migration coordinator. It retains only the exact source fence, one
///     contiguous physical cursor, a rolling digest/count, and the latest source record identity.
/// </summary>
sealed class FindingMigrationAggregate : Aggregate
{
    public const string Area = "finding-migrations";
    const string AuditSkipped = "audit_skipped";
    const string FindingImported = "finding_imported";
    static readonly string[] FindingEventKinds = ["finding_raised", "finding_revised",
        "corrective_action_added", "corrective_action_completed", "finding_acceptance_linked",
        "finding_closed", "finding_reopened"];

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    FindingMigrationFence? _fence;
    ulong _nextSourceOffset;
    ulong _importedFindingEventCount;
    string _sourceDigest = FindingMigrationDigest.Empty;
    FindingMigrationProgressed? _lastProgress;
    bool _isComplete;

    public FindingMigrationAggregate(Uuid tenantId, Uuid programId)
        : base(programId, new EventStreamAddress(tenantId.ToString(), Area, programId.ToString()))
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty)
            throw new ArgumentException("A finding migration stream needs tenant and program IDs.");

        _tenantId = tenantId;
        _programId = programId;
        On<FindingMigrationStarted>(ApplyStarted);
        On<FindingMigrationProgressed>(ApplyProgress);
        On<FindingMigrationCompleted>(ApplyCompleted);
    }

    public bool IsStarted => _fence is not null;
    public bool IsComplete => _isComplete;
    public ulong NextSourceOffset => _nextSourceOffset;
    public ulong FenceMarkerResourceOffset => _fence?.MarkerResourceOffset ?? 0;
    public ulong PreFenceProgramVersion => _fence?.PreFenceProgramVersion ?? 0;
    public ulong FenceMarkerProgramVersion => _fence?.MarkerProgramVersion ?? 0;
    public Uuid FenceEventId => _fence?.MarkerEventId ?? Uuid.Empty;
    public Uuid MigrationId => _fence?.MigrationId ?? Uuid.Empty;
    public ulong ImportedFindingEventCount => _importedFindingEventCount;
    public string SourceDigest => _sourceDigest;
    public FindingMigrationFence? SourceFence => _fence;

    public bool HasProgressed(DomainEventRecord sourceRecord) =>
        _fence is { } fence && _lastProgress is { } last &&
        sourceRecord.ResourceOffset == last.SourceOffset &&
        ReadProgressEntry(fence, sourceRecord) is { } entry && SameSourcePosition(last, entry);

    public CommandFailure? Start(FindingMigrationFence fence, ActorReference actor,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(fence);
        if (fence.TenantId != _tenantId || fence.ProgramId != _programId ||
            fence.MigrationId == Uuid.Empty || fence.MarkerEventId == Uuid.Empty ||
            fence.PreFenceProgramVersion == ulong.MaxValue ||
            fence.MarkerResourceOffset == ulong.MaxValue ||
            fence.MarkerProgramVersion != fence.PreFenceProgramVersion + 1 ||
            !ValidActor(actor) ||
            at == default || at.Offset != TimeSpan.Zero)
            return CommandFailure.InvalidContent("The finding migration fence is invalid.");
        if (_fence is { } existing)
            return existing == fence
                ? null
                : CommandFailure.StateConflict(
                    "A different source fence has already started this finding migration.");

        RaiseEvent(new FindingMigrationStarted(_tenantId, _programId, fence.MigrationId,
            fence.MarkerEventId, fence.PreFenceProgramVersion, fence.MarkerResourceOffset,
            fence.MarkerProgramVersion, actor, at));
        return null;
    }

    /// <summary>
    ///     Persists one source cursor position. The caller must append the target marker/fact pair
    ///     before advancing a non-audit record; audit records advance this cursor without becoming
    ///     target facts. Repeating the latest exact position is idempotent after an uncertain reply.
    /// </summary>
    public CommandFailure? Advance(DomainEventRecord sourceRecord, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(sourceRecord);
        if (_fence is not { } fence)
            return CommandFailure.StateConflict("The finding migration has not started.");
        if (_isComplete)
            return CommandFailure.StateConflict("The finding migration is already complete.");
        if (at == default || at.Offset != TimeSpan.Zero)
            return CommandFailure.InvalidContent("A finding migration cursor needs a UTC time.");

        var entry = ReadProgressEntry(fence, sourceRecord);
        if (entry is null)
            return CommandFailure.InvalidContent(
                "The source record is outside the fenced finding migration history.");
        if (_lastProgress is { } last && sourceRecord.ResourceOffset == last.SourceOffset)
            return SameSourcePosition(last, entry)
                ? null
                : CommandFailure.StateConflict(
                    "The source offset was already recorded with different lineage.");
        if (sourceRecord.ResourceOffset != _nextSourceOffset)
            return CommandFailure.StateConflict(
                "Finding migration progress must advance one contiguous source offset at a time.");

        var importedCount = entry.IsAudit
            ? _importedFindingEventCount
            : checked(_importedFindingEventCount + 1);
        var digest = entry.IsAudit
            ? _sourceDigest
            : FindingMigrationDigest.Append(_sourceDigest, sourceRecord.ResourceOffset,
                entry.EventId, entry.ProgramVersion, entry.OccurredAt, entry.FindingId!.Value,
                entry.EventKind!, entry.PayloadSha256!);
        var progress = new FindingMigrationProgressed(_tenantId, _programId, fence.MigrationId,
            sourceRecord.ResourceOffset, entry.EventId, entry.ProgramVersion, entry.OccurredAt,
            entry.IsAudit, entry.FindingId, entry.EventKind, entry.PayloadSha256,
            checked(sourceRecord.ResourceOffset + 1), importedCount, digest, at);
        if (!CanApplyProgress(progress, out var failure))
            return failure;
        RaiseEvent(progress);
        return null;
    }

    public CommandFailure? Complete(Uuid fenceEventId, ulong finalSourceOffset,
        ulong importedFindingEventCount, string sourceDigest, ActorReference actor,
        DateTimeOffset at)
    {
        if (_fence is not { } fence)
            return CommandFailure.StateConflict("The finding migration has not started.");
        if (_isComplete)
            return fenceEventId == fence.MarkerEventId &&
                   finalSourceOffset == fence.MarkerResourceOffset &&
                   importedFindingEventCount == _importedFindingEventCount &&
                   StringComparer.Ordinal.Equals(sourceDigest, _sourceDigest)
                ? null
                : CommandFailure.StateConflict(
                    "The finding migration was completed with different source parity.");
        var completed = new FindingMigrationCompleted(_tenantId, _programId, fence.MigrationId,
            fenceEventId, finalSourceOffset, importedFindingEventCount, sourceDigest, actor, at);
        if (!CanApplyCompleted(completed, out var failure))
            return failure;
        RaiseEvent(completed);
        return null;
    }

    void ApplyStarted(FindingMigrationStarted ev)
    {
        if (Version != 0 || _fence is not null || _tenantId != ev.TenantId ||
            _programId != ev.ProgramId ||
            ev.MigrationId == Uuid.Empty || ev.FenceEventId == Uuid.Empty ||
            ev.PreFenceProgramVersion == ulong.MaxValue ||
            ev.FenceMarkerResourceOffset == ulong.MaxValue ||
            ev.FenceMarkerProgramVersion != ev.PreFenceProgramVersion + 1 ||
            !ValidActor(ev.StartedBy) ||
            ev.StartedAt == default || ev.StartedAt.Offset != TimeSpan.Zero)
            throw InvalidHistory("The finding migration start record is invalid.");
        _fence = new FindingMigrationFence(ev.TenantId, ev.ProgramId, ev.MigrationId,
            ev.FenceEventId, ev.PreFenceProgramVersion, ev.FenceMarkerResourceOffset,
            ev.FenceMarkerProgramVersion);
    }

    void ApplyProgress(FindingMigrationProgressed ev)
    {
        if (!CanApplyProgress(ev, out _))
            throw InvalidHistory("The finding migration cursor is invalid or non-contiguous.");
        _nextSourceOffset = ev.NextSourceOffset;
        _importedFindingEventCount = ev.ImportedFindingEventCount;
        _sourceDigest = ev.SourceDigest;
        _lastProgress = ev;
    }

    void ApplyCompleted(FindingMigrationCompleted ev)
    {
        if (!CanApplyCompleted(ev, out _))
            throw InvalidHistory("The finding migration completion record is invalid.");
        _isComplete = true;
    }

    bool CanApplyProgress(FindingMigrationProgressed ev, out CommandFailure? failure)
    {
        failure = null;
        if (_fence is not { } fence || _isComplete || ev.TenantId != _tenantId ||
            ev.ProgramId != _programId || ev.MigrationId != fence.MigrationId ||
            ev.SourceOffset != _nextSourceOffset ||
            ev.SourceOffset >= fence.MarkerResourceOffset ||
            ev.SourceOffset == ulong.MaxValue || ev.NextSourceOffset != ev.SourceOffset + 1 ||
            ev.SourceEventId == Uuid.Empty ||
            !ev.SourceIsAudit && ev.SourceProgramVersion == 0 ||
            ev.SourceProgramVersion > fence.PreFenceProgramVersion ||
            _importedFindingEventCount == ulong.MaxValue ||
            ev.SourceOccurredAt == default || ev.SourceOccurredAt.Offset != TimeSpan.Zero ||
            ev.ProgressedAt == default || ev.ProgressedAt.Offset != TimeSpan.Zero ||
            !FindingMigrationDigest.IsDigest(ev.SourceDigest))
        {
            failure = CommandFailure.StateConflict(
                "Finding migration progress must match the next fenced source offset.");
            return false;
        }

        if (ev.SourceIsAudit)
        {
            if (ev.FindingId is not null || ev.SourceEventKind is not null ||
                ev.SourceEventSha256 is not null ||
                ev.ImportedFindingEventCount != _importedFindingEventCount ||
                !StringComparer.Ordinal.Equals(ev.SourceDigest, _sourceDigest))
            {
                failure = CommandFailure.InvalidContent(
                    "Audit source records advance only the physical cursor and retain no domain fact.");
                return false;
            }
            return true;
        }

        if (ev.FindingId is not { } findingId || findingId == Uuid.Empty ||
            ev.SourceEventKind is not { } kind || !FindingEventKinds.Contains(kind) ||
            !FindingHistoryEventPayloads.IsHash(ev.SourceEventSha256) ||
            ev.ImportedFindingEventCount != _importedFindingEventCount + 1 ||
            !StringComparer.Ordinal.Equals(ev.SourceDigest,
                FindingMigrationDigest.Append(_sourceDigest, ev.SourceOffset, ev.SourceEventId,
                    ev.SourceProgramVersion, ev.SourceOccurredAt, findingId, kind,
                    ev.SourceEventSha256!)))
        {
            failure = CommandFailure.InvalidContent(
                "A finding source record needs complete lineage and the next source digest.");
            return false;
        }
        return true;
    }

    bool CanApplyCompleted(FindingMigrationCompleted ev, out CommandFailure? failure)
    {
        failure = null;
        if (_fence is not { } fence || _isComplete || ev.TenantId != _tenantId ||
            ev.ProgramId != _programId || ev.MigrationId != fence.MigrationId ||
            ev.FenceEventId != fence.MarkerEventId ||
            ev.FenceMarkerResourceOffset != fence.MarkerResourceOffset ||
            _nextSourceOffset != fence.MarkerResourceOffset ||
            ev.ImportedFindingEventCount != _importedFindingEventCount ||
            !StringComparer.Ordinal.Equals(ev.SourceDigest, _sourceDigest) ||
            !FindingMigrationDigest.IsDigest(ev.SourceDigest) ||
            !ValidActor(ev.CompletedBy) ||
            ev.CompletedAt == default || ev.CompletedAt.Offset != TimeSpan.Zero)
        {
            failure = CommandFailure.StateConflict(
                "Finding migration parity can be sealed only at the exact source fence.");
            return false;
        }
        return true;
    }

    static FindingMigrationProgressEntry? ReadProgressEntry(FindingMigrationFence fence,
        DomainEventRecord record)
    {
        if (!fence.IsBeforeMarker(record))
            return null;
        var metadata = record.Event.Metadata;
        if (metadata.IsAudit)
            return new FindingMigrationProgressEntry(metadata.EventId, metadata.AggregateVersion,
                metadata.OccurredOn, true, null, null, null);

        if (!FindingHistoryEventPayloads.TryIdentify(record.Event, fence.TenantId,
                fence.ProgramId, out var findingId, out var payload))
            return null;
        return new FindingMigrationProgressEntry(metadata.EventId, metadata.AggregateVersion,
            metadata.OccurredOn, false, findingId, payload.Kind,
            FindingHistoryEventPayloads.Hash(payload.Json));
    }

    static bool SameSourcePosition(FindingMigrationProgressed last,
        FindingMigrationProgressEntry entry) => last.SourceEventId == entry.EventId &&
        last.SourceProgramVersion == entry.ProgramVersion &&
        last.SourceOccurredAt == entry.OccurredAt && last.SourceIsAudit == entry.IsAudit &&
        last.FindingId == entry.FindingId && last.SourceEventKind == entry.EventKind &&
        last.SourceEventSha256 == entry.PayloadSha256;

    static InvalidOperationException InvalidHistory(string message) => new(message);

    static bool ValidActor(ActorReference? actor) => actor is not null &&
        !string.IsNullOrWhiteSpace(actor.Kind) && !string.IsNullOrWhiteSpace(actor.Id) &&
        !string.IsNullOrWhiteSpace(actor.Display);

    sealed record FindingMigrationProgressEntry(Uuid EventId, ulong ProgramVersion,
        DateTimeOffset OccurredAt, bool IsAudit, Uuid? FindingId, string? EventKind,
        string? PayloadSha256);
}

static class FindingMigrationDigest
{
    public static string Empty { get; } = new('0', 64);

    public static string Append(string digest, ulong sourceOffset, Uuid sourceEventId,
        ulong sourceProgramVersion, DateTimeOffset sourceOccurredAt, Uuid findingId,
        string sourceEventKind, string sourcePayloadSha256)
    {
        if (!IsDigest(digest) || sourceEventId == Uuid.Empty || findingId == Uuid.Empty ||
            !FindingHistoryEventPayloads.IsHash(sourcePayloadSha256))
            throw new ArgumentException("Finding migration digest input is invalid.");
        var input = string.Join('\n', "finding-migration-v1", digest,
            sourceOffset.ToString(CultureInfo.InvariantCulture), sourceEventId.ToString(),
            sourceProgramVersion.ToString(CultureInfo.InvariantCulture),
            sourceOccurredAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            findingId.ToString(), sourceEventKind, sourcePayloadSha256);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }

    public static bool IsDigest(string? digest) => digest is { Length: 64 } &&
        digest.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
