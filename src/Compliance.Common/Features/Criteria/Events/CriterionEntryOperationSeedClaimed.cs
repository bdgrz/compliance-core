using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>Fixes one entry operation identity to an exact configured seed intent.</summary>
/// <remarks>Only a trusted bootstrap with a source-verified edition binding may produce this fact.</remarks>
[Discriminator("bdgrz.criteria.entry-operation-seed-claimed", 1)]
public sealed record CriterionEntryOperationSeedClaimed(CriterionChangeIntent Intent, long ExecutionGeneration,
    Uuid IncarnationId, Uuid? ParentAcceptedEventId, Uuid EditionRequestEventId,
    string SeedVersion, string SeedFingerprint) : DomainEvent;
