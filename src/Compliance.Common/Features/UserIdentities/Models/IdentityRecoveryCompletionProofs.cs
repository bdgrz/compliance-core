using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.UserIdentities.Models;

/// <summary>The replacement registration and the old revocation that prove an accepted swap finished.</summary>
public sealed record IdentityRecoveryCompletionProofs(SourceFactProof<UserIdentityRegistered> Registration,
    SourceFactProof<UserIdentityRevoked> Revocation)
{
    /// <summary>Schema of the proofs content.</summary>
    public const string Schema = "identity-recovery-completion-proofs.v1";
}
