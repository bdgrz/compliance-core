using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Reconciles an edition abort only after its exact witness fence is committed.</summary>
[Discriminator("bdgrz.criteria.complete-publication-abort", 1)]
sealed record CompleteCriteriaPublicationAbort(Uuid EditionId, Uuid AttemptId, string Identifier,
    ulong SourcePosition, Uuid SourceEventId) : IRequest;
