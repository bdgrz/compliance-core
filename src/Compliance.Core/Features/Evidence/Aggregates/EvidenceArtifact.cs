using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     Owns one evidence artifact through the M0-D16 lifecycle. Content stays unavailable until a clean inspection;
///     malware quarantines it until an attributed release, and a detected secret or invalid upload is rejected for
///     good.
/// </summary>
public sealed class EvidenceArtifact : Aggregate
{
    readonly Uuid _tenantId;
    EvidenceArtifactRegistered? _registration;
    EvidenceArtifactQuarantineReleased? _quarantineRelease;
    long _revision;

    public bool IsCreated => _registration is not null;
    public long Revision => _revision;
    public EvidenceArtifactContent? Content => _registration?.Content;
    public string? ContentSha256 => _registration?.ContentSha256;
    public long? ContentLength => _registration?.ContentLength;
    internal EvidenceArtifactRegistered? Registration => _registration;
    public string? State { get; private set; }
    public string? StateReason { get; private set; }
    public Uuid? QuarantineReleaseRequestId => _quarantineRelease?.RequestId;
    public EvidenceArtifactQuarantineReleased? QuarantineRelease => _quarantineRelease;

    public EvidenceArtifact(Uuid tenantId, Uuid id)
        : base(id, EvidenceStreams.Address(tenantId, id))
    {
        _tenantId = tenantId;
        On<EvidenceArtifactRegistered>(ev =>
        {
            _registration = ev;
            State = EvidenceArtifactStates.PendingInspection;
            _revision = 1;
        });
        On<EvidenceArtifactInspected>(ev =>
        {
            State = ev.State;
            StateReason = ev.Reason;
            _revision++;
        });
        On<EvidenceArtifactDownloadIssued>(ApplyDownload);
        On<EvidenceArtifactQuarantineReleased>(ApplyQuarantineRelease);
    }

    public Result<EvidenceArtifactRegistration> Register(EvidenceArtifactContent requested, string sha256,
        long length, ActorReference collector, DateTimeOffset registeredAt)
    {
        var content = EvidenceRules.Normalize(requested);
        var error = EvidenceRules.Validate(content, sha256, length);
        if (error is not null)
            return Result<EvidenceArtifactRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                error));
        if (_registration is not null)
            return _registration.Content == content && _registration.ContentSha256 == sha256 &&
                   _registration.ContentLength == length
                ? Result<EvidenceArtifactRegistration>.Success(new EvidenceArtifactRegistration(Id))
                : Result<EvidenceArtifactRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The evidence artifact already exists with different content."));
        RaiseEvent(new EvidenceArtifactRegistered(_tenantId, Id, content, sha256, length, collector,
            registeredAt));
        return Result<EvidenceArtifactRegistration>.Success(new EvidenceArtifactRegistration(Id));
    }

    public CommandFailure? RecordInspection(EvidenceInspectionOutcome outcome, DateTimeOffset inspectedAt)
    {
        if (!IsCreated)
            return CommandFailure.MissingRecord("The evidence artifact was not found.");
        if (State != EvidenceArtifactStates.PendingInspection)
            return CommandFailure.StateConflict("The evidence artifact has already been inspected.");
        var (state, reason) = outcome switch
        {
            EvidenceInspectionOutcome.Clean => (EvidenceArtifactStates.Available, (string?)null),
            EvidenceInspectionOutcome.Malware => (EvidenceArtifactStates.Quarantined, EvidenceArtifactStates.Malware),
            EvidenceInspectionOutcome.SecretDetected =>
                (EvidenceArtifactStates.Rejected, EvidenceArtifactStates.SecretDetected),
            EvidenceInspectionOutcome.Invalid => (EvidenceArtifactStates.Rejected, EvidenceArtifactStates.Invalid),
            _ => (null, null),
        };
        if (state is null)
            return CommandFailure.InvalidContent("The inspection outcome is not recognized.");
        RaiseEvent(new EvidenceArtifactInspected(_tenantId, Id, state, reason, inspectedAt));
        return null;
    }

    public CommandFailure? RecordDownload(long expectedRevision, Uuid requestId, string purpose,
        ActorReference issuedBy, DateTimeOffset issuedAt, string contentSha256, long contentLength)
    {
        if (!IsCreated || _registration is null)
            return CommandFailure.MissingRecord("The evidence artifact was not found.");
        if (State != EvidenceArtifactStates.Available || StateReason is not null)
            return CommandFailure.StateConflict("Only available evidence can be downloaded.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("evidence artifact",
                _revision));
        purpose = purpose?.Trim() ?? string.Empty;
        if (requestId == Uuid.Empty || string.IsNullOrWhiteSpace(purpose) ||
            purpose.Length > EvidenceRules.MaximumTextLength ||
            issuedBy is not { Kind: "member", Id.Length: > 0, Display.Length: > 0 and <= 200 } ||
            !Uuid.TryParse(issuedBy.Id, out var memberId) || memberId == Uuid.Empty ||
            issuedAt == default || issuedAt < _registration.RegisteredAt ||
            contentSha256 != _registration.ContentSha256 || contentLength != _registration.ContentLength)
            return CommandFailure.InvalidContent(
                "A download must identify the current artifact content, purpose, request, and member actor.");
        RaiseEvent(new EvidenceArtifactDownloadIssued(_tenantId, Id, _revision + 1, requestId,
            contentSha256, contentLength, purpose, issuedBy, issuedAt));
        return null;
    }

    void ApplyDownload(EvidenceArtifactDownloadIssued ev)
    {
        if (!IsCreated || _registration is null || ev.TenantId != _tenantId || ev.ArtifactId != Id ||
            ev.Revision != _revision + 1 || ev.RequestId == Uuid.Empty ||
            State != EvidenceArtifactStates.Available || StateReason is not null ||
            ev.ContentSha256 != _registration.ContentSha256 || ev.ContentLength != _registration.ContentLength ||
            string.IsNullOrWhiteSpace(ev.Purpose) || ev.Purpose.Trim() != ev.Purpose ||
            ev.Purpose.Length > EvidenceRules.MaximumTextLength ||
            ev.IssuedBy is not { Kind: "member", Id.Length: > 0, Display.Length: > 0 and <= 200 } ||
            !Uuid.TryParse(ev.IssuedBy.Id, out var memberId) || memberId == Uuid.Empty ||
            ev.IssuedAt == default || ev.IssuedAt < _registration.RegisteredAt)
            throw new InvalidOperationException("The evidence artifact download event is invalid.");
        _revision = ev.Revision;
    }

    // Only a malware quarantine can be released; a detected secret or invalid upload never becomes available.
    public CommandFailure? ReleaseQuarantine(long expectedRevision, Uuid requestId,
        DateTimeOffset rescannedAt, string rationale, ActorReference decidedBy,
        DateTimeOffset decidedAt)
    {
        if (!IsCreated)
            return CommandFailure.MissingRecord("The evidence artifact was not found.");
        if (_quarantineRelease is { } prior)
            return prior.RequestId == requestId && prior.Rationale == rationale?.Trim() &&
                   prior.DecidedBy == decidedBy && prior.RescanState == "clean"
                ? null
                : CommandFailure.StateConflict("The quarantine release decision is immutable.");
        if (State != EvidenceArtifactStates.Quarantined)
            return CommandFailure.StateConflict("Only a quarantined evidence artifact can be released.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("evidence artifact",
                _revision));
        rationale = rationale?.Trim() ?? string.Empty;
        if (requestId == Uuid.Empty || string.IsNullOrWhiteSpace(rationale) ||
            rationale.Length > EvidenceRules.MaximumTextLength || decidedBy is not
            { Kind: "member", Id.Length: > 0, Display.Length: > 0 and <= 200 } ||
            rescannedAt == default || decidedAt == default || rescannedAt > decidedAt)
            return CommandFailure.InvalidContent(
                "A quarantine release requires a clean attributed rescan and a bounded rationale.");
        RaiseEvent(new EvidenceArtifactQuarantineReleased(_tenantId, Id, _revision + 1,
            requestId, "clean", rescannedAt, rationale, decidedBy, decidedAt));
        return null;
    }

    void ApplyQuarantineRelease(EvidenceArtifactQuarantineReleased ev)
    {
        if (ev.TenantId != _tenantId || ev.ArtifactId != Id || !IsCreated ||
            ev.Revision != _revision + 1 || ev.RequestId == Uuid.Empty ||
            State != EvidenceArtifactStates.Quarantined || StateReason != EvidenceArtifactStates.Malware ||
            ev.RescanState != "clean" || ev.RescannedAt == default || ev.DecidedAt == default ||
            ev.RescannedAt > ev.DecidedAt || string.IsNullOrWhiteSpace(ev.Rationale) ||
            ev.Rationale.Trim() != ev.Rationale || ev.Rationale.Length > EvidenceRules.MaximumTextLength ||
            ev.DecidedBy is not { Kind: "member", Id.Length: > 0, Display.Length: > 0 and <= 200 })
            throw new InvalidOperationException("The evidence quarantine release event is invalid.");
        _quarantineRelease = ev;
        State = EvidenceArtifactStates.Available;
        StateReason = null;
        _revision = ev.Revision;
    }
}
