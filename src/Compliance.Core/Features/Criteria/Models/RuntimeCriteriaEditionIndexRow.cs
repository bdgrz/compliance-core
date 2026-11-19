using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>Edition lifecycle and scalar projection coverage, never entry contents.</summary>
sealed record RuntimeCriteriaEditionIndexRow(string Generation, Uuid EditionId,
    Uuid DraftGeneration, string Status, string? Label, Uuid? PublicationEventId,
    DateTimeOffset? PublishedAt, long PendingBeforeCut, long PublicationOrdinal = 0,
    Uuid? DraftRequestEventId = null, string? Framework = null, string? CoverageNote = null,
    string? SourceUrl = null, string? ContentRights = null, bool? NumberedCriteriaComplete = null,
    string? SeedVersion = null, string? SeedFingerprint = null, long Revision = 0,
    ulong? PublicationResourceOffset = null, Uuid? AbandonmentEventId = null,
    ulong? AbandonmentResourceOffset = null, bool IsActiveDraft = false, Uuid? SeedImportCompletionEventId = null);
