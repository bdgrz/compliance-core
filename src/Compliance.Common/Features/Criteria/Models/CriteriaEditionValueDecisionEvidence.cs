using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>One observed edition source position and proposal position for a value decision.</summary>
/// <remarks>
/// Basis is draft, publication or abandonment. A draft head becomes independently
/// useful only after edition source coverage is verified through the committed
/// outcome and any intervening terminal edition decision is resolved.
/// </remarks>
public sealed record CriteriaEditionValueDecisionEvidence(string Basis, Uuid EditionEventId,
    ulong EditionResourceOffset, ulong EditionAreaOffset, ulong ProposalAreaOffset);
