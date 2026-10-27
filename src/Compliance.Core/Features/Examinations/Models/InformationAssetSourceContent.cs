using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.Examinations.Models;

sealed record InformationAssetSourceContent(Uuid TenantId, Uuid InformationAssetId, long Revision,
    InformationAssetContent Content, ActorReference Actor, DateTimeOffset ChangedAt);
