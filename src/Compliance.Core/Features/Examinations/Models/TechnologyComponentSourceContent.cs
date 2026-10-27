using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.Examinations.Models;

sealed record TechnologyComponentSourceContent(Uuid TenantId, Uuid ComponentId, long Revision,
    TechnologyComponentContent Content, ActorReference Actor, DateTimeOffset ChangedAt);
