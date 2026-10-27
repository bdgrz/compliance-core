using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

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
