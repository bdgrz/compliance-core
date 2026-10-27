using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Models;

public sealed record CampaignWaiverView(Uuid WaiverId, Uuid PersonId, string Reason,
    DateOnly ExpiresOn, ActorReference Approver, DateTimeOffset ApprovedAt);
