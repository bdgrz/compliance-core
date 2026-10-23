using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The content a restoration completion carries as its eligibility proofs: the exact source restoration fact
///     it settles. The completion receiver still reads the member and the administrators-team assignment itself, so
///     the proofs name what was applied and never stand in for current eligibility.
/// </summary>
public sealed record AdministratorEligibilityProofs(SourceFactLocator Restoration)
{
    /// <summary>Schema of the proofs content.</summary>
    public const string Schema = "administrator-eligibility-proofs.v1";
}
