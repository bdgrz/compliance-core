using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>Settles the protection of one identity slot after the recovery completed or its preparation was rejected.</summary>
[Discriminator("bdgrz.identity-recovery.settle-protection", 1)]
public sealed record SettleRecoveryIdentityProtection(Uuid CommandId, SettleRecoveryIdentityProtectionSourceProof Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid ChallengeId,
    Uuid IdentityId)
    : IIdentityRecoveryStepReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
