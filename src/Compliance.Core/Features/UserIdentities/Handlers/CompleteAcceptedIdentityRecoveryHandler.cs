using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

/// <summary>
///     Completes the accepted swap once the same-account replacement registration and the revocation of the old
///     identity to that replacement are proven, including a registration or revocation that predates the recovery.
/// </summary>
public sealed class CompleteAcceptedIdentityRecoveryHandler(IdentityRecoveryReceiverSupport support)
    : IRequestHandler<CompleteAcceptedIdentityRecovery, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<CompleteAcceptedIdentityRecovery> context, CancellationToken ct)
    {
        var request = context.Request;
        var verified = await support.VerifyRecoveryFactAsync(request.Source, request, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        var intent = await support.ReadIntentAsync(request, ct).ConfigureAwait(false);
        if (!intent.IsSuccess)
            return Result<ReceiverOutcome>.Failure(intent.Error!);
        var frozen = intent.Value!;
        var target = IdentityRecoveryReceiverSupport.RecoveryOf(request.ChallengeId).Stream;
        if (request.Target != target || request.AccountId != frozen.AccountId)
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>("The command does not target this recovery.");
        var registration = await support.VerifySourceAsync(request.ReplacementRegistrationProof, ct).ConfigureAwait(false);
        if (!registration.IsSuccess)
            return Result<ReceiverOutcome>.Failure(registration.Error!);
        var revocation = await support.VerifySourceAsync(request.OldRevocationProof, ct).ConfigureAwait(false);
        if (!revocation.IsSuccess)
            return Result<ReceiverOutcome>.Failure(revocation.Error!);
        if (registration.Value!.Source != IdentityRecoveryReceiverSupport.IdentityStream(frozen.ReplacementIdentityId) ||
            registration.Value.Facts.UserId != frozen.AccountId ||
            revocation.Value!.Source != IdentityRecoveryReceiverSupport.IdentityStream(frozen.OldIdentityId) ||
            revocation.Value.Facts.UserId != frozen.AccountId ||
            revocation.Value.Facts.ReplacementIdentityId != frozen.ReplacementIdentityId)
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>(
                "The proofs are not the replacement registration and the old revocation of this recovery.");

        var provenance = IdentityRecoveryReceiverSupport.Provenance(request,
            nameof(CompleteAcceptedIdentityRecovery), target, frozen, verified.Value!.EventId);
        return await support.ApplyAsync<IdentityRecovery, ImmutableContentRef>(
            () => IdentityRecoveryReceiverSupport.RecoveryOf(request.ChallengeId, request.CommandId),
            current => current.Acceptance, AcceptedOperationBinding.Of(provenance),
            async (_, cancel) => Result<ImmutableContentRef>.Success(await support.Content.WriteJsonAsync(
                IdentityRecoveryReceiverSupport.Realm, Uuid.CreateVersion5(request.CommandId, IdentityRecoveryCompletionProofs.Schema),
                IdentityRecoveryCompletionProofs.Schema,
                new IdentityRecoveryCompletionProofs(request.ReplacementRegistrationProof, request.OldRevocationProof), cancel)
                .ConfigureAwait(false)),
            (_, _) => null,
            (current, proofs) => current.Complete(provenance, request.Intent, proofs), context, ct)
            .ConfigureAwait(false);
    }
}
