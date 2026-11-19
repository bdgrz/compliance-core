using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Reconciles a committed witness pin to its exact publication attempt.</summary>
[Discriminator("bdgrz.criteria.complete-publication", 1)]
sealed record CompleteCriteriaPublication(Uuid EditionId, Uuid AttemptId, string Identifier,
    ulong SourcePosition, Uuid SourceEventId) : IRequest;
