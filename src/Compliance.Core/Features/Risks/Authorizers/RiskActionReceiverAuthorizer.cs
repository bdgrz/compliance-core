using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Authorizers;

/// <summary>Only the trusted process worker may dispatch the risk action completion review receivers on actions.</summary>
sealed class RiskActionReceiverAuthorizer : IRequestAuthorizer<IRiskActionReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IRiskActionReceiver> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the internal risk action review process may dispatch treatment action receivers.")));
}
