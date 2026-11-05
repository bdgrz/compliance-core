using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class DesignatePrimaryOrgAdminHandler(IAggregateExecutor executor, IAggregateReader reader)
    : IRequestHandler<DesignatePrimaryOrgAdmin>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<DesignatePrimaryOrgAdmin> context, CancellationToken ct)
    {
        var request = context.Request;
        var tenant = await reader.HydrateAsync(new Tenant(request.TenantId), ct).ConfigureAwait(false);
        if (RequestActor.IsSystem(context.Actor) && request.UserId != tenant.OwnerUserId)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Bootstrap can designate only the organization's creator."));
        return await executor.ExecuteAsync(new PrimaryOrgAdmins(request.TenantId, request.CommandId),
            owner => AggregateOutcome.CommitOnSuccess(
                !owner.HasDesignation(request.CommandId) && (!tenant.IsRegistered || tenant.IsSuspended || tenant.IsOffboarding)
                    ? Result.Failure(new RequestError(RequestErrorKind.Conflict, "The organization cannot designate a primary administrator."))
                    : owner.Designate(request.CommandId, request.UserId,
                        request.MembershipEpisodeId, RbacOperationActor.From(context.Actor))), context, ct).ConfigureAwait(false);
    }
}
