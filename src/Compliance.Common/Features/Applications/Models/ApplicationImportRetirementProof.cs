using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>The retirement evidence one retire row of a target group requires, frozen with the group.</summary>
public sealed record ApplicationImportRetirementProof(Uuid RowId, Uuid SourceClaimId, string ProposalSha256,
    string ImpactDigest)
{
    /// <summary>Schema of one item of the required retirement proofs selection.</summary>
    public const string ItemSchema = "import-retirement-proof.v1";
}
