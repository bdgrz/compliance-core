using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>Protects one identity slot of a prepared recovery so no competing association can take it before the swap is accepted.</summary>
[Discriminator("bdgrz.identity-recovery.protect-recovery-identity", 1)]
public sealed record ProtectRecoveryIdentity(Uuid CommandId, SourceFactProof<IdentityRecoveryPrepared> Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid ChallengeId,
    Uuid AccountId, Uuid OldIdentityId, Uuid ReplacementIdentityId, Uuid IdentityId, RecoveryIdentityRole IdentityRole)
    : IIdentityRecoveryStepReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
