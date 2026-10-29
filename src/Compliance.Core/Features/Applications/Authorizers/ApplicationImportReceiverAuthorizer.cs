using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
namespace Bdgrz.Compliance.Features.Applications.Authorizers;

/// <summary>Only the trusted process worker may dispatch import receivers.</summary>
sealed class ApplicationImportReceiverAuthorizer : IRequestAuthorizer<IApplicationImportReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IApplicationImportReceiver> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the internal application import process may dispatch import receivers.")));
}
