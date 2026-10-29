using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Contracts;
using Bdgrz.Compliance.Features.UserIdentities.Events;
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
