using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Events;

[Discriminator("bdgrz.inventory.information_asset.retired", 1)]
public sealed record InformationAssetRetired(Uuid TenantId, Uuid InformationAssetId, long Revision,
    InformationAssetContent Content, DateTimeOffset EffectiveAt, string Reason, ActorReference Actor, DateTimeOffset ChangedAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse) : DomainEvent, IInformationAssetFact;
