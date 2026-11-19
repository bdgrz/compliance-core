using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>One observed edition-source fact used to check value-decision provenance.</summary>
sealed record RuntimeCriteriaEditionFactIndexRow(string Generation, Uuid EditionId, Uuid EventId,
    ulong ResourceOffset, long ObservationOrdinal, bool IsTerminal, bool IsActiveDraft);
