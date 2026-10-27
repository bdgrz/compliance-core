using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Delivery outcomes are recorded by the trusted process principal only; no actor decides what the relay did.</summary>
public sealed class EmailChallengeDeliveryOutcomeAuthorizer : IRequestAuthorizer<IEmailChallengeDeliveryOutcome>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<IEmailChallengeDeliveryOutcome> context,
        CancellationToken ct) => ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "Only the email challenge delivery process may record delivery outcomes.")));
}
