using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed record TechnologyComponentSourceContent(Uuid TenantId, Uuid ComponentId, long Revision,
    TechnologyComponentContent Content, ActorReference Actor, DateTimeOffset ChangedAt);
