using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed record InformationAssetSourceContent(Uuid TenantId, Uuid InformationAssetId, long Revision,
    InformationAssetContent Content, ActorReference Actor, DateTimeOffset ChangedAt);
