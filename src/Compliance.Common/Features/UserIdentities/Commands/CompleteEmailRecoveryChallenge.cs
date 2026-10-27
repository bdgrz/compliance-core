using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>Completes the original email challenge of a finished recovery without clearing a newer challenge slot.</summary>
[Discriminator("bdgrz.identity-recovery.complete-email-challenge", 1)]
public sealed record CompleteEmailRecoveryChallenge(Uuid CommandId, SourceFactProof<IdentityRecoveryCompleted> Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid ChallengeId)
    : IIdentityRecoveryStepReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
