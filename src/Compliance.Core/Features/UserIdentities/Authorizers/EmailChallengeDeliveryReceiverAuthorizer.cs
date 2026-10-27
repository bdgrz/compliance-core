using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Authorizers;

/// <summary>The receivers of the email challenge delivery process run as the trusted process principal only.</summary>
public sealed class EmailChallengeDeliveryReceiverAuthorizer : IRequestAuthorizer<IEmailChallengeDeliveryReceiver>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IEmailChallengeDeliveryReceiver> context,
        CancellationToken ct) => ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "Email challenge delivery receivers run only as the system process.")));
}
