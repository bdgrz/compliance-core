using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Owns one tenant-scoped instance identity and its immutable application parent.</summary>
public sealed class DeclaredSystemInstance : Aggregate
{
    readonly Uuid _tenantId;
    bool _created;
    Uuid _applicationId;
    string? _name;
    string? _kind;
    string? _accessBoundaryReference;
    string? _sourceIdentifier;
    long _revision;
    bool _retired;
    Uuid _registeredByMemberId;

    public bool IsCreated => _created;
    public bool IsRetired => _retired;
    public Uuid ApplicationId => _applicationId;
    public long Revision => _revision;
    public string? Name => _name;
    public string? Kind => _kind;
    public string? AccessBoundaryReference => _accessBoundaryReference;
    public string? SourceIdentifier => _sourceIdentifier;

    /// <summary>The member who registered the instance; separation of duties excludes them from scope approval.</summary>
    public Uuid RegisteredByMemberId => _registeredByMemberId;

    public DeclaredSystemInstance(Uuid tenantId, Uuid instanceId)
        : base(instanceId, new EventStreamAddress(tenantId.ToString(), "system-instances",
            instanceId.ToString()))
    {
        _tenantId = tenantId;
        On<SystemInstanceRegistered>(ev =>
        {
            _created = true;
            _applicationId = ev.ApplicationId;
            _name = ev.Name;
            _kind = ev.Kind;
            _accessBoundaryReference = ev.AccessBoundaryReference;
            _sourceIdentifier = ev.SourceIdentifier;
            _revision = ev.Revision;
            _registeredByMemberId = ev.ActorMemberId;
        });
        On<SystemInstanceRetired>(ApplyRetirement);
        On<SystemInstanceRevised>(ApplyRevision);
    }

    /// <summary>Retires the instance from an effective date; its history and scope decisions remain.</summary>
    public CommandFailure? Retire(long expectedRevision, DateTimeOffset effectiveAt,
        string reason, Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The system instance was not found.");
        if (_retired)
            return CommandFailure.StateConflict("The system instance is retired.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(
                VersionedRecordRules.StaleRevision("system instance", _revision));
        reason = reason?.Trim() ?? string.Empty;
        actorDisplay = actorDisplay?.Trim() ?? string.Empty;
        if (effectiveAt == default || reason.Length is 0 or > 2000 || actorMemberId == Uuid.Empty ||
            actorDisplay.Length is 0 or > 200 || changedAt == default)
            return CommandFailure.InvalidContent(
                "A retirement requires an effective date, bounded reason, and attributed actor.");
        RaiseEvent(new SystemInstanceRetired(_tenantId, _applicationId, Id, _revision + 1,
            effectiveAt, reason, actorMemberId, actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return null;
    }

    public Result<SystemInstanceRegistration> Declare(Uuid applicationId, string name, string kind,
        string? accessBoundaryReference, string? sourceIdentifier, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset changedAt)
    {
        if (applicationId == Uuid.Empty || Id == Uuid.Empty ||
            string.IsNullOrWhiteSpace(name) || name.Length > 200 ||
            string.IsNullOrWhiteSpace(kind) || kind.Length > 200 ||
            accessBoundaryReference is { Length: > 2000 } ||
            sourceIdentifier is { Length: > 1000 })
            return Result<SystemInstanceRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "A system instance requires its own identity, name, kind, and a bounded boundary reference."));

        if (_created)
            return IsSameDeclaration(_applicationId, _name!, _kind!, _accessBoundaryReference,
                    _sourceIdentifier, applicationId, name, kind, accessBoundaryReference,
                    sourceIdentifier)
                ? Result<SystemInstanceRegistration>.Success(new SystemInstanceRegistration(Id))
                : Result<SystemInstanceRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The system instance already exists with different content."));

        RaiseEvent(new SystemInstanceRegistered(_tenantId, applicationId, Id, 1,
            name.Trim(), kind.Trim(), NormalizeOptional(accessBoundaryReference),
            NormalizeOptional(sourceIdentifier),
            actorMemberId, actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return Result<SystemInstanceRegistration>.Success(new SystemInstanceRegistration(Id));
    }

    public CommandFailure? Revise(long expectedRevision, string name, string? sourceIdentifier,
        string? accessBoundaryReference, string rationale, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset changedAt)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The system instance was not found.");
        if (_retired)
            return CommandFailure.StateConflict("A retired system instance cannot be revised.");

        name = name?.Trim() ?? string.Empty;
        sourceIdentifier = NormalizeOptional(sourceIdentifier);
        accessBoundaryReference = NormalizeOptional(accessBoundaryReference);
        rationale = rationale?.Trim() ?? string.Empty;
        actorDisplay = actorDisplay?.Trim() ?? string.Empty;
        if (_name == name && _sourceIdentifier == sourceIdentifier &&
            _accessBoundaryReference == accessBoundaryReference)
            return null;
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(
                VersionedRecordRules.StaleRevision("system instance", _revision));
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 ||
            sourceIdentifier is { Length: > 1000 } || accessBoundaryReference is { Length: > 2000 } ||
            string.IsNullOrWhiteSpace(rationale) || rationale.Length > 2000 ||
            actorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(actorDisplay) ||
            actorDisplay.Length > 200 || changedAt == default)
            return CommandFailure.InvalidContent(
                "A system instance revision requires a bounded name, rationale, and named actor.");

        RaiseEvent(new SystemInstanceRevised(_tenantId, _applicationId, Id, _revision + 1,
            name, _kind!, accessBoundaryReference, sourceIdentifier, rationale,
            actorMemberId, actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return null;
    }

    void ApplyRetirement(SystemInstanceRetired ev)
    {
        var reason = ev.Reason?.Trim() ?? string.Empty;
        var actorDisplay = ev.ActorDisplay?.Trim() ?? string.Empty;
        if (!_created || _retired || ev.TenantId != _tenantId || ev.ApplicationId != _applicationId ||
            ev.SystemInstanceId != Id || ev.Revision != _revision + 1 || ev.EffectiveAt == default ||
            reason.Length is 0 or > 2000 || ev.Reason != reason || ev.ActorMemberId == Uuid.Empty ||
            actorDisplay.Length is 0 or > 200 || ev.ActorDisplay != actorDisplay || ev.ChangedAt == default ||
            ev.Actor != ActorReference.ForMember(ev.ActorMemberId, actorDisplay))
            throw new InvalidOperationException("The system instance retirement event is invalid.");
        _revision = ev.Revision;
        _retired = true;
    }

    void ApplyRevision(SystemInstanceRevised ev)
    {
        var name = ev.Name?.Trim() ?? string.Empty;
        var kind = ev.Kind?.Trim() ?? string.Empty;
        var boundary = NormalizeOptional(ev.AccessBoundaryReference);
        var source = NormalizeOptional(ev.SourceIdentifier);
        var rationale = ev.Rationale?.Trim() ?? string.Empty;
        var actorDisplay = ev.ActorDisplay?.Trim() ?? string.Empty;
        if (!_created || ev.TenantId != _tenantId || ev.ApplicationId != _applicationId ||
            ev.SystemInstanceId != Id || ev.Revision != _revision + 1 || _retired ||
            string.IsNullOrWhiteSpace(name) || name.Length > 200 ||
            string.IsNullOrWhiteSpace(kind) || kind.Length > 200 ||
            boundary is { Length: > 2000 } || source is { Length: > 1000 } ||
            string.IsNullOrWhiteSpace(rationale) || rationale.Length > 2000 ||
            ev.ActorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(actorDisplay) ||
            actorDisplay.Length > 200 || ev.ChangedAt == default ||
            ev.Actor != ActorReference.ForMember(ev.ActorMemberId, actorDisplay))
            throw new InvalidOperationException("The system instance revision event is invalid.");
        _name = name;
        _kind = kind;
        _accessBoundaryReference = boundary;
        _sourceIdentifier = source;
        _revision = ev.Revision;
    }

    /// <summary>Compares a recorded declaration with a requested one after normalization.</summary>
    public static bool IsSameDeclaration(Uuid recordedApplicationId, string recordedName,
        string recordedKind, string? recordedBoundary, string? recordedSource,
        Uuid applicationId, string? name, string? kind, string? accessBoundaryReference,
        string? sourceIdentifier) =>
        recordedApplicationId == applicationId && recordedName == name?.Trim() &&
        recordedKind == kind?.Trim() &&
        recordedBoundary == NormalizeOptional(accessBoundaryReference) &&
        recordedSource == NormalizeOptional(sourceIdentifier);

    static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
