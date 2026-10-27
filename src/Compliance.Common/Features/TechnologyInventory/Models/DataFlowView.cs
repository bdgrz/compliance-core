using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Models;

/// <summary>A data-flow version as recorded in the governed manual inventory.</summary>
public sealed record DataFlowView(Uuid TenantId, Uuid DataFlowId, long Revision,
    DataFlowContent Content, string SourceKind, ActorReference LastChangedBy,
    DateTimeOffset LastChangedAt);
