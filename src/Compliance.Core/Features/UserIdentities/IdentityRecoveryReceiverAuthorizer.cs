using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     The receivers of the identity recovery process run as the trusted process principal only; the challenge they
///     act for is carried by their verified source proof and frozen intent.
/// </summary>
public sealed class IdentityRecoveryReceiverAuthorizer : IRequestAuthorizer<IIdentityRecoveryReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IIdentityRecoveryReceiver> context, CancellationToken ct) =>
        ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Identity recovery receivers run only as the system process.")));
}
