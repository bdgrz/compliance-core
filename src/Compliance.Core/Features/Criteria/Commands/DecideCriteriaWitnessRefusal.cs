using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Attempts an exact qualified target-side witness refusal.</summary>
[Discriminator("bdgrz.criteria.decide-witness-refusal", 1)]
sealed record DecideCriteriaWitnessRefusal(Uuid EditionId, Uuid AttemptId, string Identifier,
    ulong SourcePosition, Uuid SourceEventId) : IRequest;
