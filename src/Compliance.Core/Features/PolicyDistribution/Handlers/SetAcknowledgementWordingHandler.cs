using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Versioning.Services;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Handlers;

/// <summary>Rechecks current Org Admin authority before an accepted retry or a new choice.</summary>
public sealed class SetAcknowledgementWordingHandler(IAggregateReader reader, IAggregateExecutor executor,
    IPermissionAuthorizer permissions, TimeProvider clock) : IRequestHandler<SetAcknowledgementWording>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<SetAcknowledgementWording> context, CancellationToken ct)
    {
        var request = context.Request;
        if (RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Denied();
        var now = clock.GetUtcNow();
        var memberId = RbacIds.Member(request.TenantId, userId);
        var member = await reader.HydrateAsync(Member.ForVerification(request.TenantId, memberId), ct).ConfigureAwait(false);
        if (member.UserId != userId || !member.IsAccessEligibleOn(DateOnly.FromDateTime(now.UtcDateTime)) ||
            member.Affiliation == "firm_staff" || !await permissions.IsAllowedAsync(request.TenantId, userId, memberId,
                RbacPermissions.TenantRbacManage, ct).ConfigureAwait(false))
            return Denied();
        var actor = Bdgrz.Compliance.Features.AccessControl.Models.ActorReference.ForMember(memberId,
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
        return await executor.ExecuteAsync(new AcknowledgementWording(request.TenantId, request.CommandId),
            owner => CommandFailureRequestAdapter.ToOutcome(owner.Set(request, actor, now)), context, ct).ConfigureAwait(false);
    }

    static Result Denied() => Result.Failure(new RequestError(RequestErrorKind.Forbidden,
        "Only a current Org Admin may set organization acknowledgement wording."));
}
