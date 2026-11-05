using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class RecordPrimaryMarkOutcomeHandler(IAggregateExecutor executor, IAggregateReader reader)
    : IRequestHandler<RecordPrimaryMarkOutcome>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RecordPrimaryMarkOutcome> context, CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Only member outcome reactions may complete a designation."));
        var request = context.Request;
        var member = await reader.HydrateAsync(new Member(request.TenantId, request.UserId), ct).ConfigureAwait(false);
        var outcome = member.PrimaryOutcome(request.DesignationId);
        var refusal = outcome is MemberPrimaryMarkRefused refused ? refused.Reason : null;
        var matches = outcome switch
        {
            MemberMarkedPrimary marked => marked.TenantId == request.TenantId && marked.UserId == request.UserId &&
                marked.MemberId == member.Id && marked.MembershipEpisodeId == request.MembershipEpisodeId,
            MemberPrimaryMarkRefused rejected => rejected.TenantId == request.TenantId && rejected.UserId == request.UserId &&
                rejected.MemberId == member.Id && rejected.MembershipEpisodeId == request.MembershipEpisodeId,
            _ => false,
        };
        if (!matches)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The member has not recorded its primary mark outcome.", true));
        return await executor.ExecuteAsync(new PrimaryOrgAdmins(request.TenantId),
            owner => AggregateOutcome.CommitOnSuccess(owner.RecordOutcome(request.DesignationId, request.UserId,
                request.MembershipEpisodeId, refusal)), context, ct).ConfigureAwait(false);
    }
}
