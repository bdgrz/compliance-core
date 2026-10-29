using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.Examinations.Models;

sealed record DataFlowSourceContent(Uuid TenantId, Uuid DataFlowId, long Revision,
    DataFlowContent Content, ActorReference Actor, DateTimeOffset ChangedAt);
