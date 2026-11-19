using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>A source-derived edition status and its independent catalog coverage.</summary>
/// <param name="EditionId">The platform edition identity.</param>
/// <param name="DraftGeneration">The edition's draft generation.</param>
/// <param name="Status">The projected lifecycle, such as draft or published.</param>
/// <param name="Label">The current confirmed edition label, when available.</param>
/// <param name="PublicationEventId">The committed publication fact identifying the cut, when observed.</param>
/// <param name="PublishedAt">The source publication time, when observed.</param>
/// <param name="Coverage">Whether observed pre-publication outcomes are settled as of the projection checkpoint; not a current-head or framework support claim. The fixed seed remains pending until its import completion is projected.</param>
/// <param name="Framework">The source-recorded framework, when metadata is available.</param>
/// <param name="CoverageNote">A source-recorded description of catalog coverage.</param>
/// <param name="SourceUrl">The source-recorded external reference, when supplied.</param>
/// <param name="ContentRights">The source-recorded content rights, when supplied.</param>
/// <param name="NumberedCriteriaComplete">Whether the source claims numbered-criteria coverage, not full framework support.</param>
public sealed record RuntimeCriteriaEdition(Uuid EditionId, Uuid DraftGeneration, string Status,
    string? Label, Uuid? PublicationEventId, DateTimeOffset? PublishedAt, string Coverage,
    string? Framework = null, string? CoverageNote = null, string? SourceUrl = null,
    string? ContentRights = null, bool? NumberedCriteriaComplete = null);
