using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>Sends the completion notice independently of the finished recovery under the at-least-once notice policy.</summary>
[Discriminator("bdgrz.identity-recovery.send-notice", 1)]
public sealed record SendIdentityRecoveryNotice(Uuid CommandId, SourceFactProof<IdentityRecoveryCompleted> Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid ChallengeId,
    string Recipient)
    : IIdentityRecoveryStepReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
