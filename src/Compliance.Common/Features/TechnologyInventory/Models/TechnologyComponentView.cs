using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Models;

/// <summary>A component revision as recorded in the governed manual inventory.</summary>
public sealed record TechnologyComponentView(Uuid TenantId, Uuid ComponentId, long Revision,
    TechnologyComponentContent Content, string SourceKind, ActorReference LastChangedBy,
    DateTimeOffset LastChangedAt);
