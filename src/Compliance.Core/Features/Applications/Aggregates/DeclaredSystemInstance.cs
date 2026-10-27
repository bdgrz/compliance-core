using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Aggregates;

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

    /// <summary>Stream area of every declared system instance (the realm is the tenant).</summary>
    public const string Area = "system-instances";

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    public DeclaredSystemInstance(Uuid tenantId, Uuid instanceId, Uuid selectedLogicalId = default)
        : base(instanceId, new EventStreamAddress(tenantId.ToString(), Area,
            instanceId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<SystemInstanceRegistered>(ev =>
        {
            Acceptance.Observe(ev);
            RequireOwnOperation(ev.Operation);
            if (_created)
                throw new InvalidOperationException("The system instance is already declared.");
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

    /// <summary>
    ///     Checks the retirement guards against hydrated state without raising anything; the first half of the
    ///     native acceptance pair.
    /// </summary>
    public CommandFailure? PrepareRetire(Uuid applicationId, long expectedRevision, DateTimeOffset effectiveAt,
        string reason, Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt)
    {
        if (!_created || _applicationId != applicationId)
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
        return null;
    }

    /// <summary>Retires the instance from an effective date as the native fact of <paramref name="operation" />.</summary>
    public CommandFailure? Retire(OperationProvenance operation, ImmutableContentRef firstResponse, Uuid applicationId,
        long expectedRevision, DateTimeOffset effectiveAt,
        string reason, Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        var owned = CheckOwnOperation(operation);
        if (!owned.IsSuccess)
            return CommandFailure.InvalidContent(owned.Error!.Message!);
        var check = PrepareRetire(applicationId, expectedRevision, effectiveAt, reason, actorMemberId, actorDisplay,
            changedAt);
        if (check is not null)
            return check;
        reason = reason.Trim();
        actorDisplay = actorDisplay.Trim();
        RaiseEvent(new SystemInstanceRetired(_tenantId, _applicationId, Id, _revision + 1,
            effectiveAt, reason, actorMemberId, actorDisplay, changedAt, operation, firstResponse)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return null;
    }

    /// <summary>Checks the declaration guards against hydrated state without raising anything.</summary>
    public Result PrepareDeclare(Uuid applicationId, string name, string kind, string? accessBoundaryReference,
        string? sourceIdentifier)
    {
        if (applicationId == Uuid.Empty || Id == Uuid.Empty ||
            string.IsNullOrWhiteSpace(name) || name.Length > 200 ||
            string.IsNullOrWhiteSpace(kind) || kind.Length > 200 ||
            accessBoundaryReference is { Length: > 2000 } ||
            sourceIdentifier is { Length: > 1000 })
            return Result.Failure(new RequestError(
                RequestErrorKind.Validation,
                "A system instance requires its own identity, name, kind, and a bounded boundary reference."));
        return _created
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The system instance already exists with different content."))
            : Result.Success;
    }

    /// <summary>Accepts the declaration as the native fact of <paramref name="operation" />.</summary>
    public Result Declare(OperationProvenance operation, ImmutableContentRef firstResponse, Uuid applicationId,
        string name, string kind, string? accessBoundaryReference, string? sourceIdentifier, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset changedAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        var owned = CheckOwnOperation(operation);
        if (!owned.IsSuccess)
            return owned;
        var prepared = PrepareDeclare(applicationId, name, kind, accessBoundaryReference, sourceIdentifier);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new SystemInstanceRegistered(_tenantId, applicationId, Id, 1,
            name.Trim(), kind.Trim(), NormalizeOptional(accessBoundaryReference),
            NormalizeOptional(sourceIdentifier),
            actorMemberId, actorDisplay, changedAt, operation, firstResponse)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return Result.Success;
    }

    /// <summary>True when a revision would leave the recorded content unchanged; such a revision records nothing.</summary>
    public bool IsUnchanged(string name, string? sourceIdentifier, string? accessBoundaryReference) =>
        _created && !_retired && _name == (name?.Trim() ?? string.Empty) &&
        _sourceIdentifier == NormalizeOptional(sourceIdentifier) &&
        _accessBoundaryReference == NormalizeOptional(accessBoundaryReference);

    /// <summary>
    ///     Checks the revision guards against hydrated state without raising anything; the first half of the native
    ///     acceptance pair.
    /// </summary>
    public CommandFailure? PrepareRevise(Uuid applicationId, long expectedRevision, string name,
        string? sourceIdentifier, string? accessBoundaryReference, string rationale, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset changedAt)
    {
        if (!_created || _applicationId != applicationId)
            return CommandFailure.MissingRecord("The system instance was not found.");
        if (_retired)
            return CommandFailure.StateConflict("A retired system instance cannot be revised.");
        name = name?.Trim() ?? string.Empty;
        sourceIdentifier = NormalizeOptional(sourceIdentifier);
        accessBoundaryReference = NormalizeOptional(accessBoundaryReference);
        rationale = rationale?.Trim() ?? string.Empty;
        actorDisplay = actorDisplay?.Trim() ?? string.Empty;
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
        return null;
    }

    /// <summary>Accepts the revision as the native fact of <paramref name="operation" />.</summary>
    public CommandFailure? Revise(OperationProvenance operation, ImmutableContentRef firstResponse, Uuid applicationId,
        long expectedRevision, string name, string? sourceIdentifier,
        string? accessBoundaryReference, string rationale, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset changedAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        var owned = CheckOwnOperation(operation);
        if (!owned.IsSuccess)
            return CommandFailure.InvalidContent(owned.Error!.Message!);
        var check = PrepareRevise(applicationId, expectedRevision, name, sourceIdentifier, accessBoundaryReference,
            rationale, actorMemberId, actorDisplay, changedAt);
        if (check is not null)
            return check;
        RaiseEvent(new SystemInstanceRevised(_tenantId, _applicationId, Id, _revision + 1,
            name.Trim(), _kind!, NormalizeOptional(accessBoundaryReference), NormalizeOptional(sourceIdentifier),
            rationale.Trim(), actorMemberId, actorDisplay.Trim(), changedAt, operation, firstResponse)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay.Trim()),
        });
        return null;
    }

    Result CheckOwnOperation(OperationProvenance operation) => operation.LogicalId == Uuid.Empty ||
        operation.Owner != Stream
        ? Result.Failure(new RequestError(RequestErrorKind.Validation,
            "A system instance operation requires its own logical operation."))
        : Result.Success;

    void RequireOwnOperation(OperationProvenance operation)
    {
        if (!CheckOwnOperation(operation).IsSuccess)
            throw new InvalidOperationException("The system instance fact does not carry an operation of this owner.");
    }

    void ApplyRetirement(SystemInstanceRetired ev)
    {
        Acceptance.Observe(ev);
        RequireOwnOperation(ev.Operation);
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
        Acceptance.Observe(ev);
        RequireOwnOperation(ev.Operation);
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

    static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
