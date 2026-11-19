using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>Exact observed edition and proposal positions for one entry decision.</summary>
/// <remarks>
/// Basis is <c>draft</c>, <c>publication</c>, or <c>abandonment</c>. A draft
/// observation names the exact edition head seen by a qualified reader; it is
/// not independently authoritative until a reader verifies edition coverage
/// through the committed entry-decision position and any intervening cut.
/// </remarks>
public sealed record CriteriaEntryDecisionEvidence(string Basis, Uuid EditionEventId,
    ulong EditionResourceOffset, ulong EditionAreaOffset, ulong ProposalAreaOffset);
