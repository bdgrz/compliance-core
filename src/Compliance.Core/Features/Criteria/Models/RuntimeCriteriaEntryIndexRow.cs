using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>One independently keyed entry and at most its own pending proposal.</summary>
sealed record RuntimeCriteriaEntryIndexRow(string Generation, Uuid EditionId, string Identifier,
    Uuid DraftGeneration, bool Included, Uuid? IncarnationId, string? Kind, string? Category,
    string? ParentIdentifier, Uuid? ParentIncarnationId, string? Summary,
    RuntimeCriteriaPendingIndex? Pending, Uuid? PinnedAttemptId = null,
    long PinnedAttemptOrdinal = 0, Uuid? PinnedIncarnationId = null, Uuid? PinEventId = null);
