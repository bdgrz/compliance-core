using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Delivers one committed label confirmation to its edition owner.</summary>
[Discriminator("bdgrz.criteria.apply-label-confirmation", 1)]
sealed record ApplyCriteriaLabelConfirmation(Uuid EditionId, Uuid OperationId, string Label,
    ulong SourcePosition, Uuid SourceEventId) : IRequest;
