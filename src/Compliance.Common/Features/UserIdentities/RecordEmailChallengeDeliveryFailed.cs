using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     One delivery attempt of an email challenge did not end in an accepted message.
///     <paramref name="Certainty" /> says whether that is known: a definite rejection proves non-acceptance, an unknown
///     result may still have reached the mailbox and is reconciled before another attempt.
/// </summary>
[Discriminator("bdgrz.email-challenge-delivery.record-failed", 1)]
public sealed record RecordEmailChallengeDeliveryFailed(Uuid CommandId, Uuid ChallengeId, Uuid AttemptId,
    string Certainty, string FailureCode, DateTimeOffset FailedAt) : IEmailChallengeDeliveryOutcome
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
