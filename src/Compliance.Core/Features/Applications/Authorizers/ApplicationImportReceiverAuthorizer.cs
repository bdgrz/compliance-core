using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Only the trusted process worker may dispatch import receivers.</summary>
sealed class ApplicationImportReceiverAuthorizer : IRequestAuthorizer<IApplicationImportReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IApplicationImportReceiver> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the internal application import process may dispatch import receivers.")));
}
