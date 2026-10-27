using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Models;

public sealed record CampaignWaiverView(Uuid WaiverId, Uuid PersonId, string Reason,
    DateOnly ExpiresOn, ActorReference Approver, DateTimeOffset ApprovedAt);
