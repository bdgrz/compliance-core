namespace Bdgrz.Compliance.Features.Remediation.Models;

/// <summary>The evidence and authored rationale of one closure-proposal revision.</summary>
public sealed record FindingClosureTerms(IReadOnlyList<FindingClosureArtifactVersion> ResolutionEvidence,
    string VerificationRationale, string Rationale);
