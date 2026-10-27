using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Exports.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Authorizers;

/// <summary>Only the trusted process worker may dispatch client independence preview receivers.</summary>
sealed class ClientIndependencePreviewReceiverAuthorizer : IRequestAuthorizer<IClientIndependencePreviewReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IClientIndependencePreviewReceiver> context,
        CancellationToken ct) => ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "Only the internal preview process may dispatch preview receivers.")));
}
