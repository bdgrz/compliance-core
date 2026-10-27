using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Events;
namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>Completes the accepted swap on the recovery owner once the replacement registration and the old revocation are proven.</summary>
[Discriminator("bdgrz.identity-recovery.complete-accepted", 1)]
public sealed record CompleteAcceptedIdentityRecovery(Uuid CommandId, SourceFactProof<IdentityRecoveryAccepted> Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid ChallengeId,
    Uuid AccountId, SourceFactProof<UserIdentityRegistered> ReplacementRegistrationProof, SourceFactProof<UserIdentityRevoked> OldRevocationProof)
    : IIdentityRecoveryStepReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
