using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>The one pending proposal for one independent metadata value owner.</summary>
sealed record RuntimeCriteriaValuePendingIndexRow(string Generation, Uuid OwnerId,
    CriteriaEditionValueIntent Intent, long ExecutionGeneration, Uuid ProposalEventId,
    bool BeforePublication, bool ClaimGenerationMatches);
