using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Commands;

/// <summary>
///     Delivers an identity recovery challenge to its verified mailbox under the at-least-once challenge policy. The
///     mailbox is read from the address the source fact belongs to, so neither the command nor the process record
///     carries it.
/// </summary>
[Discriminator("bdgrz.email-challenge-delivery.deliver-recovery", 1)]
public sealed record DeliverRecoveryChallenge(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid ChallengeId)
    : IEmailChallengeDeliveryReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
