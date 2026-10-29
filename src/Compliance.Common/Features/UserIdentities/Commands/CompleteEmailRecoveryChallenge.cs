using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Events;
namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>Completes the original email challenge of a finished recovery without clearing a newer challenge slot.</summary>
[Discriminator("bdgrz.identity-recovery.complete-email-challenge", 1)]
public sealed record CompleteEmailRecoveryChallenge(Uuid CommandId, SourceFactProof<IdentityRecoveryCompleted> Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid ChallengeId)
    : IIdentityRecoveryStepReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
