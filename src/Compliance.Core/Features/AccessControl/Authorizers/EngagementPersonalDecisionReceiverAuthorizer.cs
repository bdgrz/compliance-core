using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Only the trusted process worker may dispatch engagement personal decision receivers.</summary>
sealed class EngagementPersonalDecisionReceiverAuthorizer : IRequestAuthorizer<IEngagementPersonalDecisionReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IEngagementPersonalDecisionReceiver> context,
        CancellationToken ct) => ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "Only the internal personal decision process may dispatch its receivers.")));
}
