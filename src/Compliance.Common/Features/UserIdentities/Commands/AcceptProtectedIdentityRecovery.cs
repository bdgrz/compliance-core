using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Events;
namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>Accepts the swap once both identity slots are protected; an accepted swap completes forward.</summary>
[Discriminator("bdgrz.identity-recovery.accept-protected", 1)]
public sealed record AcceptProtectedIdentityRecovery(Uuid CommandId, SourceFactProof<IdentityRecoveryPrepared> Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid ChallengeId,
    ImmutableContentRef ProtectionProofs)
    : IIdentityRecoveryStepReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
