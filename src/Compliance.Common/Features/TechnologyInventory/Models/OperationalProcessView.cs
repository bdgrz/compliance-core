using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Models;

/// <summary>A operational process revision as recorded in the governed tenant inventory.</summary>
public sealed record OperationalProcessView(Uuid TenantId, Uuid OperationalProcessId, long Revision,
    OperationalProcessContent Content, string SourceKind, ActorReference LastChangedBy,
    DateTimeOffset LastChangedAt);
