using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     One role's definition, name and deletion. Definition is accepted under the role id as its immutable creation
///     identity; a rename or deletion is accepted under its own command id. An exact retry replays the original
///     acceptance and the same identity with different content conflicts.
/// </summary>
public sealed class Role : Aggregate
{
    readonly Uuid _tenantId;
    bool _isDefined;
    bool _isDeleted;
    string? _name;

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The role already carries the requested name; nothing changes.</summary>
    public static RequestError NameUnchanged { get; } = new(RequestErrorKind.Conflict,
        "The role already has that name.", false);

    /// <summary>The role is already deleted; nothing changes.</summary>
    public static RequestError AlreadyDeleted { get; } = new(RequestErrorKind.Conflict,
        "The role is already deleted.", false);

    public bool IsActive => _isDefined && !_isDeleted;

    public Role(Uuid tenantId, Uuid roleId, Uuid selectedCommandId = default)
        : base(roleId, new EventStreamAddress(tenantId.ToString(), "rbac-roles", roleId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedCommandId);
        On<RoleDefined>(ev =>
        {
            Accept(ev, ev.CommandId, ev.Operation);
            _isDefined = true;
            _name = ev.Name;
        });
        On<RoleRenamed>(ev =>
        {
            Accept(ev, ev.CommandId, ev.Operation);
            _name = ev.Name;
        });
        On<RoleDeleted>(ev =>
        {
            Accept(ev, ev.CommandId, ev.Operation);
            _isDeleted = true;
        });
    }

    /// <summary>Checks a definition against hydrated state without raising anything.</summary>
    public Result PrepareDefine(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A role requires a name."));
        return _isDefined
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict, "The role is already defined."))
            : Result.Success;
    }

    /// <summary>Accepts the definition as the native fact of <paramref name="operation" />.</summary>
    public Result Define(OperationProvenance operation, ImmutableContentRef firstResponse, string name)
    {
        var prepared = Prepare(operation, firstResponse, PrepareDefine(name));
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new RoleDefined(_tenantId, Id, name.Trim(), operation.LogicalId, operation, firstResponse));
        return Result.Success;
    }

    /// <summary>
    ///     Checks a rename against hydrated state without raising anything. A role that already has the name reports
    ///     <see cref="NameUnchanged" />, which dispatchers treat as a terminal no-op.
    /// </summary>
    public Result PrepareRename(string name)
    {
        if (_isDeleted)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The role is not defined."));
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A role requires a name."));
        return _isDefined && string.Equals(_name, name.Trim(), StringComparison.Ordinal)
            ? Result.Failure(NameUnchanged)
            : Result.Success;
    }

    /// <summary>Accepts the rename as the native fact of <paramref name="operation" />.</summary>
    public Result Rename(OperationProvenance operation, ImmutableContentRef firstResponse, string name)
    {
        var prepared = Prepare(operation, firstResponse, PrepareRename(name));
        if (!prepared.IsSuccess)
            return prepared;
        var normalized = name.Trim();
        // Renaming a built-in role the catalog has not defined yet defines it under the migrated name.
        RaiseEvent(_isDefined
            ? new RoleRenamed(_tenantId, Id, normalized, operation.LogicalId, operation, firstResponse)
            : new RoleDefined(_tenantId, Id, normalized, operation.LogicalId, operation, firstResponse));
        return Result.Success;
    }

    /// <summary>
    ///     Checks a deletion against hydrated state without raising anything. A role that is already deleted reports
    ///     <see cref="AlreadyDeleted" />, which dispatchers treat as a terminal no-op.
    /// </summary>
    public Result PrepareDelete()
    {
        if (BuiltInRbac.IsBuiltInRole(_tenantId, Id))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "Built-in roles cannot be deleted."));
        if (!_isDefined)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The role is not defined."));
        return _isDeleted ? Result.Failure(AlreadyDeleted) : Result.Success;
    }

    /// <summary>Accepts the deletion as the native fact of <paramref name="operation" />.</summary>
    public Result Delete(OperationProvenance operation, ImmutableContentRef firstResponse)
    {
        var prepared = Prepare(operation, firstResponse, PrepareDelete());
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new RoleDeleted(_tenantId, Id, operation.LogicalId, operation, firstResponse));
        return Result.Success;
    }

    Result Prepare(OperationProvenance operation, ImmutableContentRef firstResponse, Result prepared)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A role operation requires its own logical operation."));
        return prepared;
    }

    void Accept(DomainEvent fact, Uuid commandId, OperationProvenance operation)
    {
        OwnerProvenance.RequireOwn(this, commandId, operation);
        Acceptance.Observe(fact);
    }
}
