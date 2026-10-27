using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Registers the replacement identity for the account, or returns the proof of its existing same-account registration.</summary>
[Discriminator("bdgrz.identity-recovery.register-recovered-identity", 1)]
public sealed record RegisterRecoveredIdentity(Uuid CommandId, SourceFactProof<IdentityRecoveryAccepted> Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid ChallengeId,
    Uuid AccountId, Uuid ReplacementIdentityId, ImmutableContentRef ProtectionProofManifest)
    : IIdentityRecoveryStepReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
