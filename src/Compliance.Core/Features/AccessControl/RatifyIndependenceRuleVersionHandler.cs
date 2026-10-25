using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class RatifyIndependenceRuleVersionHandler(IndependenceRuleRatificationOperations operations,
    ProfessionalDutyAuthorityReader duties)
    : IRequestHandler<RatifyIndependenceRuleVersion, IndependenceRuleRatificationView>
{
    public async ValueTask<Result<IndependenceRuleRatificationView>> HandleAsync(IRequestContext<RatifyIndependenceRuleVersion> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result<IndependenceRuleRatificationView>.Failure(new RequestError(RequestErrorKind.Validation,
                "Ratification requires its command identity and exact authored catalog revision."));
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            throw new InvalidOperationException("IndependenceRuleRatificationAuthorizer must reject this actor.");
        // Current professional authority is required even for an original historical response.
        var currentDuty = await duties.ReadCurrentAsync(userId, FirmProfessionalDuty.RuleRatifier, null, ct).ConfigureAwait(false);
        if (currentDuty is null)
            return Result<IndependenceRuleRatificationView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The actor no longer has a current rule-ratifier designation."));
        var actor = ActorReference.ForFirmStaff(userId, UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
        return await operations.RatifyAsync(context, currentDuty, actor, ct).ConfigureAwait(false);
    }
}
