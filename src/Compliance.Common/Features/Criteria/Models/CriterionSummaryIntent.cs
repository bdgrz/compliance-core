using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>Client-authored correction of one entry's original product summary.</summary>
public sealed record CriterionSummaryIntent(Uuid EditionId, string Identifier, Uuid OperationId,
    long ExpectedRevision, string Summary);
