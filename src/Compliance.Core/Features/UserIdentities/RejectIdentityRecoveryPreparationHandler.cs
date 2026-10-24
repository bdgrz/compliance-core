using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Rejects a prepared recovery before acceptance. The named conflict is proven again against the authoritative
///     identity slot, so a rejection can only follow a definite competing association or revocation. An accepted
///     swap rejects this command and completes forward.
/// </summary>
public sealed class RejectIdentityRecoveryPreparationHandler(IdentityRecoveryReceiverSupport support)
    : IRequestHandler<RejectIdentityRecoveryPreparation, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<RejectIdentityRecoveryPreparation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Generation != IdentityRecovery.FrozenGeneration)
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>("The generation is not the frozen generation.");
        var recovery = await support.Reader.HydrateAsync(IdentityRecoveryReceiverSupport.RecoveryOf(request.ChallengeId),
            ct).ConfigureAwait(false);
        if (recovery.State == IdentityRecovery.Stage.None)
            return IdentityRecoveryReceiverSupport.Wait("The recovery is not prepared yet.");
        IdentityRecoveryIntent intent;
        IdentityRecoveryConflictProof conflict;
        try
        {
            intent = await support.Content.ReadJsonAsync<IdentityRecoveryIntent>(IdentityRecoveryReceiverSupport.Realm,
                recovery.Intent!, ct).ConfigureAwait(false);
            conflict = await support.Content.ReadJsonAsync<IdentityRecoveryConflictProof>(
                IdentityRecoveryReceiverSupport.Realm, request.ConflictProof, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>($"The content does not verify: {exception.Message}");
        }

        var slot = conflict.Role == RecoveryIdentityRole.Old ? intent.OldIdentityId : intent.ReplacementIdentityId;
        if (conflict.IdentityId != slot)
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>("The conflict does not name a slot of this recovery.");
        var identity = await support.Reader.HydrateAsync(new UserIdentity(conflict.IdentityId), ct).ConfigureAwait(false);
        var decision = identity.DecideProtection(request.ChallengeId, intent.AccountId, conflict.Role,
            intent.ReplacementIdentityId);
        var target = recovery.Stream;
        var provenance = new OperationProvenance(request.CommandId, request.ChallengeId, null,
            nameof(RejectIdentityRecoveryPreparation), intent.Header.OriginalActor, target,
            intent.ChallengeId.ToString(), null, intent.Header.AuthoredIntentSha256, intent.Header.AcceptedAt, target,
            target);
        return await support.ApplyAsync(
            () => IdentityRecoveryReceiverSupport.RecoveryOf(request.ChallengeId, request.CommandId),
            current => current.Acceptance, AcceptedOperationBinding.Of(provenance),
            current =>
            {
                if (current.State is IdentityRecovery.Stage.Accepted or IdentityRecovery.Stage.Completed)
                    return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>(
                        "An accepted swap cannot be rejected; it completes forward.");
                // A replay was already decided above; a new rejection needs the conflict proven right now.
                return decision.Outcome == UserIdentity.ProtectionOutcome.Conflict
                    ? null
                    : IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>(
                        "The named association conflict cannot be proven against the authoritative identity.");
            },
            (IdentityRecovery current) => current.Reject(provenance, recovery.Intent!, request.ConflictProof), context, ct)
            .ConfigureAwait(false);
    }
}
