using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation.Authorizers;

/// <summary>Only the trusted process worker may dispatch finding closure and child materialization receivers.</summary>
sealed class FindingReceiverAuthorizer : IRequestAuthorizer<IFindingReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IFindingReceiver> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the internal finding processes may dispatch finding receivers.")));
}
