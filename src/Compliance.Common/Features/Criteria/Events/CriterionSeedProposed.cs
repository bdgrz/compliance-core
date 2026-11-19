using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>One fixed platform seed entry proposed by the trusted migration.</summary>
[Discriminator("bdgrz.criteria.entry-seed-proposed", 1)]
public sealed record CriterionSeedProposed(CriterionChangeIntent Intent, long ExecutionGeneration,
    Uuid IncarnationId, Uuid? ParentAcceptedEventId, Uuid EditionRequestEventId, Uuid OperationClaimEventId,
    string SeedVersion, string SeedFingerprint) : DomainEvent;
