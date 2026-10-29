using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>
///     Rejects a prepared recovery before it is accepted: it fences delayed protection and acceptance of the challenge.
///     <paramref name="ConflictProof" /> is the content that names the identity slot whose association conflicts; the
///     handler proves the conflict again against the authoritative identity before it appends.
/// </summary>
[Discriminator("bdgrz.identity-recovery.reject-preparation", 1)]
public sealed record RejectIdentityRecoveryPreparation(Uuid CommandId, Uuid ChallengeId, long Generation,
    ImmutableContentRef ConflictProof) : IIdentityRecoveryReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
