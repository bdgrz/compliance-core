using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Delivers the exact committed edition abort to its witness owner.</summary>
[Discriminator("bdgrz.criteria.fence-publication-witness", 1)]
sealed record FenceCriteriaPublicationWitness(Uuid EditionId, Uuid AttemptId, string Identifier,
    ulong SourcePosition, Uuid SourceEventId) : IRequest;
