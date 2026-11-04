using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Events;

[Discriminator("bdgrz.inventory.information_asset.recorded", 1)]
public sealed record InformationAssetRecorded(Uuid TenantId, Uuid InformationAssetId, long Revision,
    InformationAssetContent Content, ActorReference Actor, DateTimeOffset ChangedAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse) : DomainEvent, IInformationAssetFact;
