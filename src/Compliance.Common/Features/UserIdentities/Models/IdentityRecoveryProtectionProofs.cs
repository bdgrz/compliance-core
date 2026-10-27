using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Events;
namespace Bdgrz.Compliance.Features.UserIdentities.Models;

/// <summary>The exact protection facts of the old and the replacement slot that precede the acceptance of a swap.</summary>
public sealed record IdentityRecoveryProtectionProofs(SourceFactProof<RecoveryIdentityProtected> Old,
    SourceFactProof<RecoveryIdentityProtected> Replacement)
{
    /// <summary>Schema of the proofs content.</summary>
    public const string Schema = "identity-recovery-protection-proofs.v1";
}
