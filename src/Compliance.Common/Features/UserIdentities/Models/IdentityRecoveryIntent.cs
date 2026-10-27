using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Models;

/// <summary>
///     The immutable candidate frozen when the recovery challenge is claimed: both proofs were verified, only
///     non-bearer provenance is retained, and a later challenge can never rewrite it.
/// </summary>
public sealed record IdentityRecoveryIntent(DurableIntentHeader Header, Uuid ChallengeId, Uuid AccountId,
    Uuid OldIdentityId, Uuid ReplacementIdentityId, string OldProvider, string OldSubject, string ReplacementProvider,
    string ReplacementSubject, ImmutableContentRef EmailProofProvenance, ImmutableContentRef ReplacementProofProvenance)
{
    /// <summary>Schema of the frozen intent content.</summary>
    public const string Schema = "identity-recovery-intent.v1";
}
