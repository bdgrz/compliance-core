using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Delivers an exact committed claim-owned refusal to its edition.</summary>
[Discriminator("bdgrz.criteria.apply-label-refusal", 1)]
sealed record ApplyCriteriaEditionLabelRefusal(Uuid EditionId, Uuid OperationId, string Label,
    ulong SourcePosition, Uuid SourceEventId) : IRequest;
