using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Commands;

/// <summary>Aborts the exact edition attempt after a committed witness refusal.</summary>
[Discriminator("bdgrz.criteria.abort-publication", 1)]
sealed record AbortCriteriaPublication(Uuid EditionId, Uuid AttemptId, string Identifier,
    ulong SourcePosition, Uuid SourceEventId) : IRequest;
