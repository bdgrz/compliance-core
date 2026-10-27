using Cntryl.Portia;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Authorizers;

/// <summary>Only the trusted process worker may dispatch the advisor feedback materialization receiver.</summary>
sealed class AdvisorReceiverAuthorizer : IRequestAuthorizer<IAdvisorReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IAdvisorReceiver> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the internal advisor feedback process may dispatch advisor receivers.")));
}
