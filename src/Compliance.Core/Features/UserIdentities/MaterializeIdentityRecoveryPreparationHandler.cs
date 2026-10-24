using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Freezes the exact candidate of the claimed challenge; a later challenge can never alter it.</summary>
public sealed class MaterializeIdentityRecoveryPreparationHandler(IdentityRecoveryReceiverSupport support)
    : IRequestHandler<MaterializeIdentityRecoveryPreparation, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<MaterializeIdentityRecoveryPreparation> context, CancellationToken ct)
    {
        var request = context.Request;
        var verified = await support.VerifySourceAsync(request.Source, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        var claimed = verified.Value!.Facts;
        if (verified.Value.Source.Realm != IdentityRecoveryReceiverSupport.Realm ||
            claimed.ChallengeId != request.ChallengeId || claimed.Operation.RootOperationId != request.OperationId ||
            claimed.Operation.LogicalId != request.OperationId || claimed.Intent != request.Intent ||
            request.Generation != IdentityRecovery.FrozenGeneration)
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>(
                "The claim does not carry this operation, generation and intent.");
        var intent = await support.ReadIntentAsync(request, ct).ConfigureAwait(false);
        if (!intent.IsSuccess)
            return Result<ReceiverOutcome>.Failure(intent.Error!);
        var frozen = intent.Value!;
        var target = IdentityRecoveryReceiverSupport.RecoveryOf(request.ChallengeId).Stream;
        if (request.Target != target || claimed.UserId != request.AccountId || frozen.AccountId != request.AccountId ||
            claimed.OldIdentityId != request.OldIdentityId || frozen.OldIdentityId != request.OldIdentityId ||
            claimed.ReplacementIdentityId != request.ReplacementIdentityId ||
            frozen.ReplacementIdentityId != request.ReplacementIdentityId ||
            claimed.VerifiedEmailProof != request.VerifiedProofProvenance ||
            frozen.EmailProofProvenance != request.VerifiedProofProvenance ||
            claimed.VerifiedReplacementProof != frozen.ReplacementProofProvenance)
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>(
                "The command does not match the claimed challenge and its frozen candidate.");

        var provenance = IdentityRecoveryReceiverSupport.Provenance(request,
            nameof(MaterializeIdentityRecoveryPreparation), target, frozen, verified.Value.EventId);
        return await support.ApplyAsync(() => IdentityRecoveryReceiverSupport.RecoveryOf(request.ChallengeId,
                request.CommandId),
            recovery => recovery.Acceptance, AcceptedOperationBinding.Of(provenance),
            _ => null,
            (IdentityRecovery recovery) => recovery.Materialize(provenance, request.Intent,
                request.Source.RequiredFacts), context, ct).ConfigureAwait(false);
    }
}
