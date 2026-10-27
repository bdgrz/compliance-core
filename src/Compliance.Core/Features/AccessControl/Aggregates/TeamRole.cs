using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class TeamRole : Aggregate
{
    readonly Uuid _tenantId;
    readonly Uuid _teamId;
    readonly Uuid _roleId;
    readonly Uuid _selectedCommandId;
    DomainEvent? _selectedFact;
    Uuid? _assignmentId;

    public bool IsAssigned => _assignmentId is not null;
    public Uuid? AssignmentId => _assignmentId;
    public Uuid TenantId => _tenantId;
    public Uuid TeamId => _teamId;
    public Uuid RoleId => _roleId;

    public TeamRole(Uuid tenantId, Uuid teamId, Uuid roleId, Uuid selectedCommandId = default)
        : base(RbacIds.TeamRole(tenantId, teamId, roleId),
            new EventStreamAddress(tenantId.ToString(), "rbac-team-roles",
                RbacIds.TeamRole(tenantId, teamId, roleId).ToString()))
    {
        _tenantId = tenantId;
        _teamId = teamId;
        _roleId = roleId;
        _selectedCommandId = selectedCommandId;
        On<TeamRoleAssigned>(Apply);
        On<TeamRoleRemoved>(Apply);
    }

    public Result Assign(Uuid commandId, ulong expectedRevision, ActorAttribution actor)
    {
        if (Validate(commandId, actor) is { } error)
            return Result.Failure(error);
        if (_selectedFact is { } prior)
            return prior is TeamRoleAssigned assigned && assigned.ExpectedRevision == expectedRevision &&
                assigned.Actor == actor ? Result.Success : ChangedIntent();
        if (Version != expectedRevision || IsAssigned)
            return Conflict("The team role changed or is already assigned.");
        RaiseEvent(new TeamRoleAssigned(_tenantId, _teamId, _roleId, commandId, expectedRevision, actor));
        return Result.Success;
    }

    public Result Remove(Uuid commandId, ulong expectedRevision, Uuid assignmentId, ActorAttribution actor)
    {
        if (Validate(commandId, actor) is { } error)
            return Result.Failure(error);
        if (assignmentId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "An assignment identity is required."));
        if (_selectedFact is { } prior)
            return prior is TeamRoleRemoved removed && removed.ExpectedRevision == expectedRevision &&
                removed.AssignmentId == assignmentId && removed.Actor == actor ? Result.Success : ChangedIntent();
        if (Version != expectedRevision || _assignmentId != assignmentId)
            return Conflict("The team role revision or assignment episode changed.");
        RaiseEvent(new TeamRoleRemoved(_tenantId, _teamId, _roleId, commandId, expectedRevision, actor, assignmentId));
        return Result.Success;
    }

    RequestError? Validate(Uuid commandId, ActorAttribution actor) =>
        commandId == Uuid.Empty || commandId != _selectedCommandId || actor is null ||
        string.IsNullOrWhiteSpace(actor.Subject) || string.IsNullOrWhiteSpace(actor.Issuer)
            ? new RequestError(RequestErrorKind.Validation, "A selected command identity and original actor are required.")
            : null;

    void Apply(TeamRoleAssigned ev)
    {
        ValidateFact(ev.TenantId, ev.TeamId, ev.RoleId, ev.CommandId, ev.ExpectedRevision, ev.Actor, ev);
        if (IsAssigned)
            throw new InvalidOperationException("An active team role cannot be assigned again.");
        _assignmentId = ev.CommandId;
    }

    void Apply(TeamRoleRemoved ev)
    {
        ValidateFact(ev.TenantId, ev.TeamId, ev.RoleId, ev.CommandId, ev.ExpectedRevision, ev.Actor, ev);
        if (_assignmentId != ev.AssignmentId || ev.AssignmentId == Uuid.Empty)
            throw new InvalidOperationException("Team role removal must close the exact active assignment.");
        _assignmentId = null;
    }

    void ValidateFact(Uuid tenantId, Uuid teamId, Uuid roleId, Uuid commandId, ulong expectedRevision,
        ActorAttribution actor, DomainEvent fact)
    {
        if (tenantId != _tenantId || teamId != _teamId || roleId != _roleId || commandId == Uuid.Empty ||
            expectedRevision != Version || actor is null || string.IsNullOrWhiteSpace(actor.Subject) ||
            string.IsNullOrWhiteSpace(actor.Issuer))
            throw new InvalidOperationException("A team role fact must match its owner, actor and original revision.");
        if (commandId != _selectedCommandId)
            return;
        if (_selectedFact is not null)
            throw new InvalidOperationException("One team role operation cannot be accepted more than once.");
        _selectedFact = fact;
    }

    static Result ChangedIntent() => Conflict("This team role command identity already has different accepted intent.");
    static Result Conflict(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
