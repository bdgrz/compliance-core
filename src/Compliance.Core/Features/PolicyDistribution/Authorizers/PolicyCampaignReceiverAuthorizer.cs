using Bdgrz.Compliance.Features.PolicyDistribution;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Authorizers;

/// <summary>Only the trusted process worker may dispatch participant and campaign closure receivers.</summary>
sealed class PolicyCampaignReceiverAuthorizer : IRequestAuthorizer<IPolicyCampaignReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IPolicyCampaignReceiver> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the internal campaign processes may dispatch participant and closure receivers.")));
}
