using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class RemoveTeamRoleHandler(IAggregateExecutor executor) : IRequestHandler<RemoveTeamRole>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RemoveTeamRole> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.TeamId == Uuid.Empty || request.RoleId == Uuid.Empty)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A tenant, team and role are required.")));
        if (request.TeamId == BuiltInRbac.AdministratorsTeamId(request.TenantId) &&
            request.RoleId == BuiltInRbac.TenantAdministrationRoleId(request.TenantId))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The Administrators team keeps the Tenant Administration role.")));
        return executor.ExecuteAsync(
            new TeamRole(request.TenantId, request.TeamId, request.RoleId, request.CommandId),
            assignment => AggregateOutcome.CommitOnSuccess(assignment.Remove(request.CommandId, request.ExpectedRevision,
                request.AssignmentId, RbacOperationActor.From(context.Actor))), context, ct);
    }
}
