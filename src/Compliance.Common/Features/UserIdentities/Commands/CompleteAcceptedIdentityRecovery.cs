using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Completes the accepted swap on the recovery owner once the replacement registration and the old revocation are proven.</summary>
[Discriminator("bdgrz.identity-recovery.complete-accepted", 1)]
public sealed record CompleteAcceptedIdentityRecovery(Uuid CommandId, SourceFactProof<IdentityRecoveryAccepted> Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid ChallengeId,
    Uuid AccountId, SourceFactProof<UserIdentityRegistered> ReplacementRegistrationProof, SourceFactProof<UserIdentityRevoked> OldRevocationProof)
    : IIdentityRecoveryStepReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
