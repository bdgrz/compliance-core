using System.Diagnostics.CodeAnalysis;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "RolePermission is the canonical RBAC relationship term.")]
public sealed class RolePermission : Aggregate
{
    readonly Uuid _tenantId;
    readonly Uuid _roleId;
    readonly string _permission;
    readonly Uuid _selectedCommandId;
    DomainEvent? _selectedFact;
    Uuid? _assignmentId;

    public bool IsAssigned => _assignmentId is not null;
    public Uuid? AssignmentId => _assignmentId;
    public Uuid TenantId => _tenantId;
    public Uuid RoleId => _roleId;
    public string Permission => _permission;

    public RolePermission(Uuid tenantId, Uuid roleId, string permission, Uuid selectedCommandId = default)
        : base(RbacIds.RolePermission(tenantId, roleId, Permissions.Normalize(permission)),
            new EventStreamAddress(tenantId.ToString(), "rbac-role-permissions",
                RbacIds.RolePermission(tenantId, roleId, Permissions.Normalize(permission)).ToString()))
    {
        _tenantId = tenantId;
        _roleId = roleId;
        _permission = Permissions.Normalize(permission);
        _selectedCommandId = selectedCommandId;
        On<RolePermissionAssigned>(Apply);
        On<RolePermissionRemoved>(Apply);
    }

    public Result Assign(Uuid commandId, ulong expectedRevision, ActorAttribution actor)
    {
        if (Validate(commandId, actor) is { } error)
            return Result.Failure(error);
        if (_selectedFact is { } prior)
            return prior is RolePermissionAssigned assigned && assigned.ExpectedRevision == expectedRevision &&
                assigned.Actor == actor ? Result.Success : ChangedIntent();
        if (Version != expectedRevision || IsAssigned)
            return Conflict("The role permission changed or is already assigned.");
        RaiseEvent(new RolePermissionAssigned(_tenantId, _roleId, _permission, commandId, expectedRevision, actor));
        return Result.Success;
    }

    public Result Remove(Uuid commandId, ulong expectedRevision, Uuid assignmentId, ActorAttribution actor)
    {
        if (Validate(commandId, actor) is { } error)
            return Result.Failure(error);
        if (assignmentId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "An assignment identity is required."));
        if (_selectedFact is { } prior)
            return prior is RolePermissionRemoved removed && removed.ExpectedRevision == expectedRevision &&
                removed.AssignmentId == assignmentId && removed.Actor == actor ? Result.Success : ChangedIntent();
        if (Version != expectedRevision || _assignmentId != assignmentId)
            return Conflict("The role permission revision or assignment episode changed.");
        RaiseEvent(new RolePermissionRemoved(_tenantId, _roleId, _permission, commandId, expectedRevision, actor,
            assignmentId));
        return Result.Success;
    }

    RequestError? Validate(Uuid commandId, ActorAttribution actor) =>
        commandId == Uuid.Empty || commandId != _selectedCommandId || actor is null ||
        string.IsNullOrWhiteSpace(actor.Subject) || string.IsNullOrWhiteSpace(actor.Issuer)
            ? new RequestError(RequestErrorKind.Validation, "A selected command identity and original actor are required.")
            : null;

    void Apply(RolePermissionAssigned ev)
    {
        ValidateFact(ev.TenantId, ev.RoleId, ev.Permission, ev.CommandId, ev.ExpectedRevision, ev.Actor, ev);
        if (IsAssigned)
            throw new InvalidOperationException("An active role permission cannot be assigned again.");
        _assignmentId = ev.CommandId;
    }

    void Apply(RolePermissionRemoved ev)
    {
        ValidateFact(ev.TenantId, ev.RoleId, ev.Permission, ev.CommandId, ev.ExpectedRevision, ev.Actor, ev);
        if (_assignmentId != ev.AssignmentId || ev.AssignmentId == Uuid.Empty)
            throw new InvalidOperationException("Permission removal must close the exact active assignment.");
        _assignmentId = null;
    }

    void ValidateFact(Uuid tenantId, Uuid roleId, string permission, Uuid commandId, ulong expectedRevision,
        ActorAttribution actor, DomainEvent fact)
    {
        if (tenantId != _tenantId || roleId != _roleId || permission != _permission || commandId == Uuid.Empty ||
            expectedRevision != Version || actor is null || string.IsNullOrWhiteSpace(actor.Subject) ||
            string.IsNullOrWhiteSpace(actor.Issuer))
            throw new InvalidOperationException("A permission fact must match its owner, actor and original revision.");
        if (commandId != _selectedCommandId)
            return;
        if (_selectedFact is not null)
            throw new InvalidOperationException("One permission operation cannot be accepted more than once.");
        _selectedFact = fact;
    }

    static Result ChangedIntent() => Conflict("This permission command identity already has different accepted intent.");
    static Result Conflict(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
