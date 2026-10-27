using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Reactors;

/// <summary>
///     Starts the email challenge delivery process when a verification or identity recovery challenge is accepted on an
///     email address. The process record, with the effect payload frozen from the accepted fact, exists before the
///     source checkpoint advances; delivery itself, and every retry of it, belongs to the process.
/// </summary>
public sealed partial class EmailChallengeDeliveryReactor(IProjectionCheckpointStore checkpoints,
    ProcessRootReaction process, IImmutableContentStore content, TimeProvider clock)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "email-addresses")),
      IReactorHandler<EmailChallengeIssued>,
      IReactorHandler<IdentityRecoveryChallengeIssued>
{
    public ValueTask HandleAsync(IReactorContext<EmailChallengeIssued> context, CancellationToken ct)
    {
        var issued = context.Trigger;
        return StartAsync(context, issued.Operation, issued.ChallengeId, issued.UserId, issued.ExpiresAt,
            issued.TokenKeyId, EmailChallengeDeliveryIntent.Verification, ct);
    }

    public ValueTask HandleAsync(IReactorContext<IdentityRecoveryChallengeIssued> context, CancellationToken ct)
    {
        var issued = context.Trigger;
        return StartAsync(context, issued.Operation, issued.ChallengeId, issued.UserId, issued.ExpiresAt,
            issued.TokenKeyId, EmailChallengeDeliveryIntent.Recovery, ct);
    }

    async ValueTask StartAsync(IReactorContext context, OperationProvenance operation, Uuid challengeId, Uuid userId,
        DateTimeOffset expiresAt, string tokenKeyId, string purpose, CancellationToken ct)
    {
        // A challenge that expired before this reaction can never be used, so nothing is delivered for it.
        if (clock.GetUtcNow() >= expiresAt)
            return;
        var intent = new EmailChallengeDeliveryIntent(new DurableIntentHeader(operation.RootOperationId,
            operation.Owner, operation.AffectedOwner, operation.ExpectedRevisionOwner, operation.Actor,
            operation.Episode, operation.OriginalExpectedRevision, operation.AuthoredIntentSha256,
            operation.AcceptedAt), challengeId, userId, purpose, expiresAt, tokenKeyId);
        // The content id derives from the source fact, so a replayed reaction freezes the identical intent.
        var frozen = await content.WriteJsonAsync(EmailChallengeDeliveryWorker.Realm,
            Uuid.CreateVersion5(context.Source.Event.Metadata.EventId, EmailChallengeDeliveryIntent.Schema),
            EmailChallengeDeliveryIntent.Schema, intent, ct).ConfigureAwait(false);
        await process.StartAsync(EmailChallengeDeliveryProcess.Name, context, frozen, [],
            nameof(EmailChallengeDeliveryReactor), ct).ConfigureAwait(false);
    }
}
