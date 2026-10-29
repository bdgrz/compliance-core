using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Models;

/// <summary>Names the identity slot whose competing association rejects a prepared recovery.</summary>
public sealed record IdentityRecoveryConflictProof(Uuid IdentityId, RecoveryIdentityRole Role, string Reason)
{
    /// <summary>Schema of the conflict content.</summary>
    public const string Schema = "identity-recovery-conflict-proof.v1";
}
