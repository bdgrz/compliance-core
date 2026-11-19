using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Fences one terminal edition request against a delayed label reservation.</summary>
[Discriminator("bdgrz.criteria.fence-edition-label-request", 1)]
sealed record FenceCriteriaEditionLabelRequest(Uuid EditionId, Uuid OperationId, string Label,
    Uuid EditionRequestEventId, ulong TerminalEventPosition, Uuid TerminalEventId) : IRequest;
