using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>
/// An immutable platform edition. ContentRights is its source-recorded content
/// declaration; SupportGaps disclose category-level limits.
/// </summary>
public sealed record CriteriaCatalogEdition(Uuid EditionId, string Framework,
    string EditionLabel, DateTimeOffset PublishedAt, bool IsComplete,
    string CoverageNote, string SourceUrl, string ContentRights,
    IReadOnlyList<CriteriaSupportGap> SupportGaps);
