using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     A population draft was opened for one exact system-instance revision. It is the root native fact of the
///     population's opening operation; the logical id of <paramref name="Operation" /> is the population id.
/// </summary>
[Discriminator("bdgrz.access_population.opened", 2)]
public sealed record AccessPopulationOpened(Uuid TenantId, Uuid PopulationId,
    Uuid ApplicationId, Uuid SystemInstanceId, long SystemInstanceRevision,
    DateTimeOffset ObservedAt, string SourceKind, string Source, ActorReference OpenedBy,
    DateTimeOffset OpenedAt, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
