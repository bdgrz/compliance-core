using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class UnmarkMemberPrimaryHandler(IAggregateExecutor executor, IAggregateReader reader) : IRequestHandler<UnmarkMemberPrimary>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<UnmarkMemberPrimary> context, CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Only release reactions may remove a primary mark."));
        var request = context.Request;
        var primaries = await reader.HydrateAsync(new PrimaryOrgAdmins(request.TenantId), ct).ConfigureAwait(false);
        if (!primaries.HasReleased(request.UserId, request.MembershipEpisodeId, request.DesignationId))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The primary designation has not been released."));
        return await executor.ExecuteAsync(new Member(request.TenantId, request.UserId),
            member => AggregateOutcome.CommitOnSuccess(member.UnmarkPrimary(request.DesignationId, request.MembershipEpisodeId)),
            context, ct).ConfigureAwait(false);
    }
}
