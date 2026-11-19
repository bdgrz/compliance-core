using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Attempts a source-qualified contention decision for one exact edition request.</summary>
[Discriminator("bdgrz.criteria.decide-label-refusal", 1)]
sealed record DecideCriteriaEditionLabelRefusal(Uuid EditionId, Uuid OperationId, string Label,
    ulong EditionEventPosition, Uuid EditionEventId) : IRequest;
