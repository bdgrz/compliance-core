using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Only the trusted process worker may dispatch engagement acceptance receivers.</summary>
sealed class EngagementAcceptanceReceiverAuthorizer : IRequestAuthorizer<IEngagementAcceptanceReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IEngagementAcceptanceReceiver> context,
        CancellationToken ct) => ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "Only the internal acceptance process may dispatch its receivers.")));
}
