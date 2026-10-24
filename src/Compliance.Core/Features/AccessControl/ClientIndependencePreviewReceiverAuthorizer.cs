using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Only the trusted process worker may dispatch client independence preview receivers.</summary>
sealed class ClientIndependencePreviewReceiverAuthorizer : IRequestAuthorizer<IClientIndependencePreviewReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IClientIndependencePreviewReceiver> context,
        CancellationToken ct) => ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "Only the internal preview process may dispatch preview receivers.")));
}
