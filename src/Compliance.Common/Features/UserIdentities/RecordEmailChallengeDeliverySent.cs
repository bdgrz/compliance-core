using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>The relay accepted the message of one delivery attempt of an email challenge.</summary>
[Discriminator("bdgrz.email-challenge-delivery.record-sent", 1)]
public sealed record RecordEmailChallengeDeliverySent(Uuid CommandId, Uuid ChallengeId, Uuid AttemptId,
    DateTimeOffset SentAt) : IEmailChallengeDeliveryOutcome
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
