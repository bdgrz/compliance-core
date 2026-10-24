using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Accepts the swap only now: both identity slots carry the exact protection proofs and are still protected.
///     A rejected preparation or a settled slot makes a delayed acceptance a terminal no-op.
/// </summary>
public sealed class AcceptProtectedIdentityRecoveryHandler(IdentityRecoveryReceiverSupport support)
    : IRequestHandler<AcceptProtectedIdentityRecovery, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<AcceptProtectedIdentityRecovery> context, CancellationToken ct)
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
        if (request.Target != target)
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>("The command does not target this recovery.");
        var proofs = await support.VerifyProtectionAsync(request.ProtectionProofs, request, frozen, ct)
            .ConfigureAwait(false);
        if (!proofs.IsSuccess)
            return Result<ReceiverOutcome>.Failure(proofs.Error!);

        var recovery = await support.Reader.HydrateAsync(IdentityRecoveryReceiverSupport.RecoveryOf(request.ChallengeId),
            ct).ConfigureAwait(false);
        var old = await support.Reader.HydrateAsync(new UserIdentity(frozen.OldIdentityId), ct).ConfigureAwait(false);
        var replacement = await support.Reader.HydrateAsync(new UserIdentity(frozen.ReplacementIdentityId), ct)
            .ConfigureAwait(false);
        var provenance = IdentityRecoveryReceiverSupport.Provenance(request, nameof(AcceptProtectedIdentityRecovery),
            target, frozen, verified.Value!.EventId);
        return await support.ApplyAsync(
            () => IdentityRecoveryReceiverSupport.RecoveryOf(request.ChallengeId, request.CommandId),
            current => current.Acceptance, AcceptedOperationBinding.Of(provenance),
            current =>
            {
                if (current.State == IdentityRecovery.Stage.Rejected || old.IsSettledFor(request.ChallengeId) ||
                    replacement.IsSettledFor(request.ChallengeId))
                    return IdentityRecoveryReceiverSupport.Superseded("The preparation was rejected or its protection settled.");
                return old.IsProtectedFor(request.ChallengeId) && replacement.IsProtectedFor(request.ChallengeId)
                    ? null
                    : IdentityRecoveryReceiverSupport.Wait("A protected slot is not readable yet.");
            },
            (IdentityRecovery current) => current.Accept(provenance, request.Intent, request.ProtectionProofs),
            context, ct).ConfigureAwait(false);
    }
}
