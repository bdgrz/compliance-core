using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Events;

/// <summary>
///     Records one complete, attributed asset revision; revision 1 creates it. The fact accepts its own logical operation, so an exact retry of the
///     creation or revision replays the frozen original response however far the record has moved since.
/// </summary>
[Discriminator("bdgrz.inventory.information_asset.revision_recorded", 1)]
public sealed record InformationAssetRevisionRecorded(Uuid TenantId, Uuid InformationAssetId, long Revision,
    InformationAssetContent Content, ActorReference Actor, DateTimeOffset ChangedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
