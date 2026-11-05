using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class ReleasePrimaryOrgAdminHandler(IAggregateExecutor executor) : IRequestHandler<ReleasePrimaryOrgAdmin>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReleasePrimaryOrgAdmin> context, CancellationToken ct)
    {
        if (RequestActor.IsSystem(context.Actor))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden, "A member must release the primary administrator.")));
        var request = context.Request;
        return executor.ExecuteAsync(new PrimaryOrgAdmins(request.TenantId, request.CommandId),
            owner => AggregateOutcome.CommitOnSuccess(owner.Release(request.CommandId, request.UserId,
                request.DesignationId, request.Reason, RbacOperationActor.From(context.Actor))), context, ct);
    }
}
