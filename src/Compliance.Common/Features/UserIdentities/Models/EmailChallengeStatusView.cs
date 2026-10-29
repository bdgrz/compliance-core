namespace Bdgrz.Compliance.Features.UserIdentities.Models;

public sealed record EmailChallengeStatusView(string DeliveryStatus, DateTimeOffset? ExpiresAt);
