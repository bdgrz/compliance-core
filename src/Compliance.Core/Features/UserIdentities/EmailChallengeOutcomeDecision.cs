namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>An outcome kind with the record it leaves behind.</summary>
public sealed record EmailChallengeOutcomeDecision(EmailChallengeOutcomeKind Kind, EmailChallengeDeliveryRecord Record);
