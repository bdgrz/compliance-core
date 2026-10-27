using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Events;
namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>Revokes the old identity to the replacement, only with the authoritative proof of the replacement registration.</summary>
[Discriminator("bdgrz.identity-recovery.revoke-recovered-old-identity", 1)]
public sealed record RevokeRecoveredOldIdentity(Uuid CommandId, SourceFactProof<IdentityRecoveryAccepted> Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid ChallengeId,
    Uuid OldIdentityId, Uuid ReplacementIdentityId, SourceFactProof<UserIdentityRegistered> ReplacementRegistrationProof)
    : IIdentityRecoveryStepReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
