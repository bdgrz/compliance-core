using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Freezes the exact both-proof-verified candidate of a claimed challenge as an immutable preparation on the recovery owner.</summary>
[Discriminator("bdgrz.identity-recovery.materialize-preparation", 1)]
public sealed record MaterializeIdentityRecoveryPreparation(Uuid CommandId, SourceFactProof<IdentityRecoveryChallengeClaimed> Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid ChallengeId,
    Uuid AccountId, Uuid OldIdentityId, Uuid ReplacementIdentityId, ImmutableContentRef VerifiedProofProvenance)
    : IIdentityRecoveryStepReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
