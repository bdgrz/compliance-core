namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>A source-backed entry outcome; only an accepted outcome carries a proof.</summary>
public sealed record CriteriaEntryAuthorityResult(CriteriaEntryAuthorityStatus Status, CriteriaEntryProof? Proof);
