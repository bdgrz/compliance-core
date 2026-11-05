using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class MarkMemberPrimaryHandler(IAggregateExecutor executor, IAggregateReader reader) : IRequestHandler<MarkMemberPrimary>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<MarkMemberPrimary> context, CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Only designation reactions may mark a member primary."));
        var request = context.Request;
        var primaries = await reader.HydrateAsync(new PrimaryOrgAdmins(request.TenantId), ct).ConfigureAwait(false);
        if (!primaries.HasPending(request.UserId, request.MembershipEpisodeId, request.DesignationId))
            return Result.Success;
        return await executor.ExecuteAsync(new Member(request.TenantId, request.UserId),
            member => AggregateOutcome.CommitOnSuccess(member.MarkPrimary(request.DesignationId, request.MembershipEpisodeId)),
            context, ct).ConfigureAwait(false);
    }
}
