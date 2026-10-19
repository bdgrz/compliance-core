using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed record DataFlowSourceContent(Uuid TenantId, Uuid DataFlowId, long Revision,
    DataFlowContent Content, ActorReference Actor, DateTimeOffset ChangedAt);
