using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>A population was attested and frozen as an immutable snapshot of its raw facts.</summary>
[Discriminator("bdgrz.access_population.accepted", 2)]
public sealed record AccessPopulationAccepted(Uuid TenantId, Uuid PopulationId,
    long Revision, Uuid SnapshotId, string ContentSha256, Uuid CalculationId, string Attestation,
    ActorReference AcceptedBy, DateTimeOffset AcceptedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
