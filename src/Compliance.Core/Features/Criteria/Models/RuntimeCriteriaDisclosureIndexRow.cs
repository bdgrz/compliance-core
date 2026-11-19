using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>One independently keyed source disclosure.</summary>
sealed record RuntimeCriteriaDisclosureIndexRow(string Generation, Uuid EditionId,
    string Category, string Code, string Note);
