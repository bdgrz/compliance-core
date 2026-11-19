using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>One value outcome whose source evidence must be reconciled before coverage is complete.</summary>
sealed record RuntimeCriteriaValueUnresolvedIndexRow(string Generation, Uuid OutcomeEventId,
    Uuid OwnerId, ulong OutcomeResourceOffset, CriteriaEditionValueIntent Intent, Uuid ProposalEventId,
    string Basis, Uuid EditionEventId, ulong EditionResourceOffset, ulong EditionAreaOffset,
    bool ClaimMatches, bool TerminalMatches);
