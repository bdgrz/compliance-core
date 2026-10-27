using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>The launched campaign, its frozen audience size and its logical revision.</summary>
public sealed record CampaignRegistration(Uuid CampaignId, int AudienceCount, long Revision);
