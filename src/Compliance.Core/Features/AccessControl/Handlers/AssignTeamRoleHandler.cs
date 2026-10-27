using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class AssignTeamRoleHandler(IAggregateExecutor executor) : IRequestHandler<AssignTeamRole>
{
    public ValueTask<Result> HandleAsync(IRequestContext<AssignTeamRole> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.TeamId == Uuid.Empty || request.RoleId == Uuid.Empty)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A tenant, team and role are required.")));
        return executor.ExecuteAsync(
            new TeamRole(request.TenantId, request.TeamId, request.RoleId, request.CommandId),
            assignment => AggregateOutcome.CommitOnSuccess(assignment.Assign(request.CommandId, request.ExpectedRevision,
                RbacOperationActor.From(context.Actor))), context, ct);
    }
}
