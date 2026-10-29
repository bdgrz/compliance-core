using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>
///     A draft's complete observed facts were replaced. <paramref name="FactsContent" /> is the complete
///     <see cref="AccessPopulationFacts" /> as verified immutable content, so no list can be truncated by an event
///     size.
/// </summary>
[Discriminator("bdgrz.access_population.facts_recorded", 2)]
public sealed record AccessPopulationFactsRecorded(Uuid TenantId,
    Uuid PopulationId, long Revision, ImmutableContentRef FactsContent, ActorReference RecordedBy,
    DateTimeOffset RecordedAt, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
