using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Models;

/// <summary>The launched campaign, its frozen audience size and its logical revision.</summary>
public sealed record CampaignRegistration(Uuid CampaignId, int AudienceCount, long Revision);
