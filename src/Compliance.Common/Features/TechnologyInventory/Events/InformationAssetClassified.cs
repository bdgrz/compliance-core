using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Events;

[Discriminator("bdgrz.inventory.information_asset.classified", 1)]
public sealed record InformationAssetClassified(Uuid TenantId, Uuid InformationAssetId, long Revision,
    InformationAssetContent Content, string PreviousClassification, string Classification, ActorReference Actor, DateTimeOffset ChangedAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse) : DomainEvent, IInformationAssetFact;
