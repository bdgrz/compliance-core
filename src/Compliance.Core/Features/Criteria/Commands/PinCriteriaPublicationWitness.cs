using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Delivers the exact committed publication start to its witness owner.</summary>
[Discriminator("bdgrz.criteria.pin-publication-witness", 1)]
sealed record PinCriteriaPublicationWitness(Uuid EditionId, Uuid AttemptId, string Identifier,
    ulong SourcePosition, Uuid SourceEventId) : IRequest;
