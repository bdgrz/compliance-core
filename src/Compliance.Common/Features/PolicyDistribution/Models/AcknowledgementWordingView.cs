using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Models;

public sealed record AcknowledgementWordingView(Uuid TenantId, long Revision, string Wording,
    ActorReference? SetBy, DateTimeOffset? SetAt);
