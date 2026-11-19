using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Evidence.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Evidence.Aggregates;

/// <summary>
///     Owns one evidence artifact through the M0-D16 lifecycle. Content stays unavailable until a clean inspection;
///     malware quarantines it until an attributed release, and a detected secret or invalid upload is rejected for
///     good.
/// </summary>
public sealed class EvidenceArtifact : Aggregate
{
    readonly Uuid _tenantId;
    EvidenceArtifactRegistered? _registration;
    EvidenceArtifactInspected? _inspection;
    EvidenceArtifactQuarantineReleased? _quarantineRelease;
    long _revision;
    readonly Uuid _selectedCommandId;
    ArtifactHandlingClassChanged? _lastHandlingClassChange;
    ArtifactHandlingClassChanged? _selectedHandlingClassChange;
    string? _handlingClass;

    public bool IsCreated => _registration is not null;
    public long Revision => _revision;
    public EvidenceArtifactContent? Content => _registration is null ? null :
        _registration.Content with { HandlingClass = _handlingClass ?? _registration.Content.HandlingClass };
    public string? ContentSha256 => _registration?.ContentSha256;
    public long? ContentLength => _registration?.ContentLength;
    internal EvidenceArtifactRegistered? Registration => _registration;
    public string? State { get; private set; }
    public string? StateReason { get; private set; }
    public Uuid? QuarantineReleaseRequestId => _quarantineRelease?.RequestId;
    public EvidenceArtifactQuarantineReleased? QuarantineRelease => _quarantineRelease;

    public EvidenceArtifact(Uuid tenantId, Uuid id, Uuid selectedCommandId = default)
        : base(id, EvidenceStreams.Address(tenantId, id))
    {
        _tenantId = tenantId;
        _selectedCommandId = selectedCommandId;
        On<EvidenceArtifactRegistered>(ev =>
        {
            _registration = ev;
            _handlingClass = ev.Content.HandlingClass;
            State = EvidenceArtifactStates.PendingInspection;
            _revision = 1;
        });
        On<EvidenceArtifactInspected>(ev =>
        {
            _inspection = ev;
            State = ev.State;
            StateReason = ev.Reason;
            _revision++;
        });
        On<EvidenceArtifactDownloadIssued>(ApplyDownload);
        On<EvidenceArtifactQuarantineReleased>(ApplyQuarantineRelease);
        On<ArtifactHandlingClassChanged>(ApplyHandlingClass);
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

    /// <summary>Changes the artifact's own handling class while preserving its immutable captured content identity.</summary>
    public CommandFailure? ChangeHandlingClass(ChangeArtifactHandlingClass request, ActorReference actor, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);
        var accepted = _selectedHandlingClassChange ?? (_lastHandlingClassChange?.CommandId == request.CommandId
            ? _lastHandlingClassChange : null);
        if (accepted is not null)
            return accepted.CommandId == request.CommandId && request.TenantId == _tenantId && request.ArtifactId == Id &&
                   request.ExpectedRevision == accepted.OriginalExpectedRevision &&
                   AuthoredIntentDigest.Compute(request) == accepted.AuthoredIntentSha256 &&
                   actor.Kind == "member" && Uuid.TryParse(actor.Id, out var retryActor) &&
                   Uuid.TryParse(accepted.ChangedBy.Id, out var originalActor) && retryActor == originalActor
                ? null : CommandFailure.StateConflict("The classification command accepted another intent or actor.");
        if (_registration is null)
            return CommandFailure.MissingRecord("The evidence artifact was not found.");
        if (State is EvidenceArtifactStates.Rejected or EvidenceArtifactStates.Disposed)
            return CommandFailure.StateConflict("Rejected or disposed evidence cannot change handling class.");
        if (request.ExpectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("evidence artifact", _revision));
        var rationale = request.Rationale?.Trim() ?? string.Empty;
        if (request.CommandId == Uuid.Empty || request.TenantId != _tenantId || request.ArtifactId != Id ||
            request.HandlingClass is not ("public" or "internal" or "confidential" or "restricted") ||
            rationale.Length is 0 or > EvidenceRules.MaximumTextLength || actor.Kind != "member" ||
            !Uuid.TryParse(actor.Id, out var member) || member == Uuid.Empty ||
            string.IsNullOrWhiteSpace(actor.Display) || actor.Display.Trim().Length > 200 ||
            at == default || at < _registration.RegisteredAt ||
            _lastHandlingClassChange is { } previous && at < previous.ChangedAt)
            return CommandFailure.InvalidContent("Classification requires its exact scope, a valid class, rationale and member attribution.");
        if (request.HandlingClass == Content!.HandlingClass)
            return null;
        RaiseEvent(new ArtifactHandlingClassChanged(_tenantId, Id, _revision + 1, request.CommandId,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), Content!.HandlingClass,
            request.HandlingClass, rationale, actor with { Display = actor.Display.Trim() }, at));
        return null;
    }

    void ApplyHandlingClass(ArtifactHandlingClassChanged ev)
    {
        if (_registration is null || ev.TenantId != _tenantId || ev.ArtifactId != Id || ev.CommandId == Uuid.Empty ||
            ev.Revision != _revision + 1 || ev.OriginalExpectedRevision != _revision ||
            State is EvidenceArtifactStates.Rejected or EvidenceArtifactStates.Disposed ||
            ev.PreviousClass != Content!.HandlingClass || ev.HandlingClass == ev.PreviousClass ||
            ev.HandlingClass is not ("public" or "internal" or "confidential" or "restricted") ||
            string.IsNullOrWhiteSpace(ev.Rationale) || ev.Rationale.Trim() != ev.Rationale ||
            ev.Rationale.Length > EvidenceRules.MaximumTextLength || ev.ChangedBy is not { Kind: "member" } ||
            !Uuid.TryParse(ev.ChangedBy.Id, out var member) || member == Uuid.Empty ||
            string.IsNullOrWhiteSpace(ev.ChangedBy.Display) || ev.ChangedBy.Display.Trim() != ev.ChangedBy.Display ||
            ev.ChangedBy.Display.Length > 200 || ev.ChangedAt == default || ev.ChangedAt < _registration.RegisteredAt ||
            _lastHandlingClassChange is { } previous && ev.ChangedAt < previous.ChangedAt ||
            ev.AuthoredIntentSha256 is not { Length: 64 })
            throw new InvalidOperationException("The evidence handling class change event is invalid.");
        _handlingClass = ev.HandlingClass;
        _lastHandlingClassChange = ev;
        if (ev.CommandId == _selectedCommandId)
        {
            if (_selectedHandlingClassChange is not null)
                throw new InvalidOperationException("The evidence handling class command is repeated.");
            _selectedHandlingClassChange = ev;
        }
        _revision = ev.Revision;
    }

    public CommandFailure? RecordInspection(EvidenceInspectionOutcome outcome, DateTimeOffset inspectedAt)
    {
        if (!IsCreated)
            return CommandFailure.MissingRecord("The evidence artifact was not found.");
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
        // The same outcome at the same instant is an exact retry of the committed inspection, even after later
        // facts such as a quarantine release or a download.
        if (_inspection is { } inspected)
            return inspected.State == state && inspected.Reason == reason && inspected.InspectedAt == inspectedAt
                ? null
                : CommandFailure.StateConflict("The evidence artifact has already been inspected.");
        if (inspectedAt == default || _registration is null || inspectedAt < _registration.RegisteredAt)
            return CommandFailure.InvalidContent("An inspection cannot precede the artifact registration.");
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
                   prior.DecidedBy == decidedBy && prior.RescanState == "clean" && prior.RescannedAt == rescannedAt &&
                   prior.DecidedAt == decidedAt
                ? null
                : CommandFailure.StateConflict("The quarantine release decision is immutable.");
        if (State != EvidenceArtifactStates.Quarantined || _inspection is null)
            return CommandFailure.StateConflict("Only a quarantined evidence artifact can be released.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("evidence artifact",
                _revision));
        rationale = rationale?.Trim() ?? string.Empty;
        if (requestId == Uuid.Empty || string.IsNullOrWhiteSpace(rationale) ||
            rationale.Length > EvidenceRules.MaximumTextLength || decidedBy is not
            { Kind: "member", Id.Length: > 0, Display.Length: > 0 and <= 200 } ||
            !Uuid.TryParse(decidedBy.Id, out var memberId) || memberId == Uuid.Empty ||
            rescannedAt == default || decidedAt == default || rescannedAt > decidedAt ||
            rescannedAt < _inspection.InspectedAt)
            return CommandFailure.InvalidContent(
                "A quarantine release requires a member decision, a clean rescan after the inspection and a bounded rationale.");
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
