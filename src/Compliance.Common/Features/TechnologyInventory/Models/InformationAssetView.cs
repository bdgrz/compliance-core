using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Models;

/// <summary>An information asset revision as recorded in the governed manual inventory.</summary>
public sealed record InformationAssetView(Uuid TenantId, Uuid InformationAssetId,
    long Revision, InformationAssetContent Content, string SourceKind,
    ActorReference LastChangedBy, DateTimeOffset LastChangedAt);
