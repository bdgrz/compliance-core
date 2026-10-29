using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Snapshots.Events;
using Bdgrz.Compliance.Features.Snapshots.Models;
namespace Bdgrz.Compliance.Features.Snapshots.Aggregates;

public sealed class ImmutableSnapshot : Aggregate
{
    readonly Uuid _tenantId;
    bool _isFrozen;
    bool _invalidHistory;
    Uuid _rootSnapshotId;
    Uuid? _amendsSnapshotId;
    Uuid _programId;
    string? _kind;
    ProgramScopeManifest? _manifest;
    string? _contentSha256;
    string? _canonicalManifest;
    string? _amendmentReason;
    Uuid _eventTenantId;
    Uuid _eventSnapshotId;
    Uuid _actorMemberId;
    string? _actorDisplay;
    DateTimeOffset _frozenAt;

    public bool IsFrozen => _isFrozen;
    public Uuid RootSnapshotId => _rootSnapshotId;
    public Uuid? AmendsSnapshotId => _amendsSnapshotId;
    public Uuid ProgramId => _programId;
    public string? Kind => _kind;
    public ProgramScopeManifest? Manifest => _manifest;
    public string? CanonicalManifest => _canonicalManifest;
    public string? ContentSha256 => _contentSha256;
    public string? AmendmentReason => _amendmentReason;
    public Uuid EventTenantId => _eventTenantId;
    public Uuid EventSnapshotId => _eventSnapshotId;
    public Uuid ActorMemberId => _actorMemberId;
    public string? ActorDisplay => _actorDisplay;
    public DateTimeOffset FrozenAt => _frozenAt;

    public ImmutableSnapshot(Uuid tenantId, Uuid snapshotId)
        : base(snapshotId, new EventStreamAddress(tenantId.ToString(), "snapshots",
            snapshotId.ToString()))
    {
        _tenantId = tenantId;
        On<SnapshotFrozen>(ev =>
        {
            if (_isFrozen)
            {
                _invalidHistory = true;
                return;
            }
            _invalidHistory = ev.TenantId != _tenantId || ev.SnapshotId != Id;
            _isFrozen = true;
            _rootSnapshotId = ev.RootSnapshotId;
            _amendsSnapshotId = ev.AmendsSnapshotId;
            _programId = ev.ProgramId;
            _kind = ev.Kind;
            _manifest = ev.Manifest;
            _contentSha256 = ev.ContentSha256;
            _canonicalManifest = ev.CanonicalManifest;
            _amendmentReason = ev.AmendmentReason;
            _eventTenantId = ev.TenantId;
            _eventSnapshotId = ev.SnapshotId;
            _actorMemberId = ev.ActorMemberId;
            _actorDisplay = ev.ActorDisplay;
            _frozenAt = ev.FrozenAt;
        });
    }

    public Result<SnapshotRegistration> Freeze(Uuid rootSnapshotId, Uuid? amendsSnapshotId,
        Uuid programId, ProgramScopeManifest manifest, string canonicalManifest,
        string contentSha256, string? amendmentReason, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset frozenAt)
    {
        if (_invalidHistory)
            return Result<SnapshotRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The snapshot history is inconsistent with its identity or contains multiple freezes."));
        amendmentReason = amendmentReason?.Trim();
        if (_tenantId == Uuid.Empty || rootSnapshotId == Uuid.Empty || programId == Uuid.Empty ||
            actorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(canonicalManifest) ||
            string.IsNullOrWhiteSpace(contentSha256) ||
            amendsSnapshotId == Uuid.Empty ||
            (amendsSnapshotId is null) != (amendmentReason is null) ||
            (amendmentReason is not null && string.IsNullOrWhiteSpace(amendmentReason)) ||
            (amendsSnapshotId is null && rootSnapshotId != Id) ||
            (amendsSnapshotId is not null &&
             (amendsSnapshotId == Id || rootSnapshotId == Id)) ||
            manifest.TenantId != _tenantId || manifest.ProgramId != programId || manifest.ProgramRevision < 1 ||
            manifest.BoundaryId == Uuid.Empty || manifest.ApprovedBoundaryVersionId == Uuid.Empty ||
            !IsLowercaseSha256(manifest.ProgramContentSha256) || !IsLowercaseSha256(manifest.BoundaryContentSha256) ||
            !SnapshotContentIdentity.MatchesManifest(manifest, canonicalManifest, contentSha256))
            return Result<SnapshotRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "The snapshot requires a complete source manifest and amendment linkage."));
        if (_isFrozen)
            return _rootSnapshotId == rootSnapshotId &&
                   _amendsSnapshotId == amendsSnapshotId &&
                   _programId == programId &&
                   _kind == "program_scope" &&
                   _manifest == manifest &&
                   _contentSha256 == contentSha256 &&
                   _canonicalManifest == canonicalManifest &&
                   _amendmentReason == amendmentReason &&
                   _actorMemberId == actorMemberId
                ? Result<SnapshotRegistration>.Success(new SnapshotRegistration(Id,
                    _contentSha256!))
                : Result<SnapshotRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The snapshot already exists with different content."));
        RaiseEvent(new SnapshotFrozen(_tenantId, Id, rootSnapshotId, amendsSnapshotId,
            programId, "program_scope", manifest, canonicalManifest, contentSha256,
            amendmentReason?.Trim(), actorMemberId, actorDisplay, frozenAt));
        return Result<SnapshotRegistration>.Success(new SnapshotRegistration(Id, contentSha256));
    }

    static bool IsLowercaseSha256(string? value) => value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
