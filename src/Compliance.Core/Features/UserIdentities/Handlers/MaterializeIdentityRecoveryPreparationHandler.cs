using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

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
        var target = IdentityRecoveryReceiverSupport.RecoveryOf(request.ChallengeId).Stream;
        // Actual source verification and every source-carried DTO binding precede receipt knowledge.
        if (verified.Value.Source.Realm != IdentityRecoveryReceiverSupport.Realm || request.Target != target ||
            claimed.ChallengeId != request.ChallengeId || claimed.Operation.RootOperationId != request.OperationId ||
            claimed.Operation.LogicalId != request.OperationId || claimed.Intent != request.Intent ||
            claimed.Operation.CommandKind != nameof(CompleteIdentityRecovery) || claimed.Operation.Owner != verified.Value.Source ||
            claimed.Operation.AffectedOwner != verified.Value.Source || claimed.Operation.ExpectedRevisionOwner != verified.Value.Source ||
            request.Generation != IdentityRecovery.FrozenGeneration || claimed.UserId != request.AccountId ||
            claimed.OldIdentityId != request.OldIdentityId || claimed.ReplacementIdentityId != request.ReplacementIdentityId ||
            claimed.VerifiedEmailProof != request.VerifiedProofProvenance || claimed.OriginalActor != claimed.Operation.Actor ||
            claimed.AcceptedAt != claimed.Operation.AcceptedAt)
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>(
                "The command does not match its exact claimed challenge and frozen candidate source.");

        var provenance = new OperationProvenance(request.CommandId, request.OperationId, verified.Value.EventId,
            nameof(MaterializeIdentityRecoveryPreparation), claimed.OriginalActor, target, claimed.Operation.Episode,
            request.ExpectedRevision is >= 0 and var revision ? (ulong)revision : null,
            claimed.Operation.AuthoredIntentSha256, claimed.AcceptedAt, target, target);
        var binding = AcceptedOperationBinding.Of(provenance);
        IdentityOwnerPreparationCut? cut = null;
        var prepared = await NativeReceiverPreparation.PrepareAsync<IdentityRecovery, IdentityRecoveryIntent>(support.Reader,
            () => IdentityRecoveryReceiverSupport.RecoveryOf(request.ChallengeId, request.CommandId),
            owner => IdentityRecoveryReceiverSupport.Decide(owner.Acceptance, binding, owner.Stream),
            async (owner, cancel) =>
            {
                var intent = await support.ReadIntentAsync(request, cancel).ConfigureAwait(false);
                if (!intent.IsSuccess)
                    return intent;
                var frozen = intent.Value!;
                if (frozen.AccountId != request.AccountId || frozen.OldIdentityId != request.OldIdentityId ||
                    frozen.ReplacementIdentityId != request.ReplacementIdentityId ||
                    frozen.EmailProofProvenance != request.VerifiedProofProvenance ||
                    claimed.VerifiedReplacementProof != frozen.ReplacementProofProvenance ||
                    frozen.Header.FirstOwner != verified.Value.Source || frozen.Header.AffectedOwner != verified.Value.Source ||
                    frozen.Header.ExpectedRevisionOwner != verified.Value.Source ||
                    frozen.Header.OriginalExpectedRevision != claimed.Operation.OriginalExpectedRevision ||
                    frozen.Header.OriginalActor != claimed.OriginalActor ||
                    frozen.Header.AuthoredIntentSha256 != claimed.Operation.AuthoredIntentSha256 ||
                    frozen.Header.AcceptedAt != claimed.AcceptedAt || frozen.Header.Episode != claimed.Operation.Episode)
                    return IdentityRecoveryReceiverSupport.Invalid<IdentityRecoveryIntent>(
                        "The frozen candidate differs from its exact accepted claim provenance.");
                cut = IdentityOwnerPreparationCut.Of(owner);
                return intent;
            }, ct).ConfigureAwait(false);
        if (!prepared.IsSuccess)
            return Result<ReceiverOutcome>.Failure(prepared.Error!);
        if (prepared.Value!.Outcome is { } known)
            return known;
        return await support.ApplyAsync(() => IdentityRecoveryReceiverSupport.RecoveryOf(request.ChallengeId,
                request.CommandId), recovery => recovery.Acceptance, binding, _ => null,
            (IdentityRecovery recovery) => recovery.Materialize(provenance, request.Intent, request.Source.RequiredFacts),
            context, ct, owner => cut!.Require(owner)).ConfigureAwait(false);
    }
}
