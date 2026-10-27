using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Only the trusted process worker may dispatch the request resolution and child materialization receivers.</summary>
sealed class AuditorRequestReceiverAuthorizer : IRequestAuthorizer<IAuditorRequestReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IAuditorRequestReceiver> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the internal auditor request processes may dispatch auditor request receivers.")));
}
