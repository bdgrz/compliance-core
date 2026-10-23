using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Settles a withdrawal on the guard once the source fact carrying its exact operation was applied. Only the
///     operation, reason and episode it names are settled; an unrelated source fact is a permanent conflict.
/// </summary>
public sealed class CompleteAdministratorWithdrawalHandler(AdministratorReceiverSupport support)
    : IRequestHandler<CompleteAdministratorWithdrawal, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<CompleteAdministratorWithdrawal> context, CancellationToken ct)
    {
        var request = context.Request;
        var source = await VerifySourceAsync(request, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<ReceiverOutcome>.Failure(source.Error!);
        var intent = await support.ReadIntentAsync(request, request.MemberEpisode, ct).ConfigureAwait(false);
        if (!intent.IsSuccess)
            return Result<ReceiverOutcome>.Failure(intent.Error!);
        var tenantId = AdministratorReceiverSupport.TenantOf(request.Target);
        var loaded = await support.Guards.LoadAsync(tenantId, request.CommandId, ct).ConfigureAwait(false);
        var guard = loaded.Guard;
        if (request.Target != guard.Stream)
            return AdministratorReceiverSupport.Invalid<ReceiverOutcome>("The command does not target this tenant's guard.");
        var provenance = AdministratorReceiverSupport.Provenance(request, nameof(CompleteAdministratorWithdrawal),
            guard.Stream, intent.Value!, request.Source.Proof.EventId);
        var result = await AdministratorTransitionEffects.UnitResultAsync(support.Content, request.Target.Realm,
            request.OperationId, ct).ConfigureAwait(false);
        return await support.ApplyAsync(loaded.Create,
            current => current.Acceptance, AcceptedOperationBinding.Of(provenance),
            current => current.PendingGeneration(request.OperationId) == request.Generation
                ? null
                : AdministratorReceiverSupport.Invalid<ReceiverOutcome>("The guard has no such unfinished withdrawal."),
            (TenantManagerGuard current) => current.CompleteWithdrawal(provenance, request.OperationId, intent.Value!.Header.AffectedOwner,
                intent.Value.Header.OriginalExpectedRevision is { } revision ? (long)revision : -1, request.Intent,
                request.Source.Proof.RequiredFacts, result), context, ct).ConfigureAwait(false);
    }

    async Task<Result> VerifySourceAsync(CompleteAdministratorWithdrawal request, CancellationToken ct)
    {
        switch (request.Source)
        {
            case CompleteAdministratorWithdrawalSourceProof.SuspendedProof suspended:
                {
                    var verified = await support.VerifySourceAsync(suspended.Fact, ct).ConfigureAwait(false);
                    return verified.IsSuccess
                        ? Check(request, verified.Value!.Facts.Operation, verified.Value.Source, verified.Value.Facts.MemberId)
                        : Result.Failure(verified.Error!);
                }
            case CompleteAdministratorWithdrawalSourceProof.DeprovisionedProof deprovisioned:
                {
                    var verified = await support.VerifySourceAsync(deprovisioned.Fact, ct).ConfigureAwait(false);
                    return verified.IsSuccess
                        ? Check(request, verified.Value!.Facts.Operation, verified.Value.Source, verified.Value.Facts.MemberId)
                        : Result.Failure(verified.Error!);
                }
            case CompleteAdministratorWithdrawalSourceProof.RemovedProof removed:
                {
                    var verified = await support.VerifySourceAsync(removed.Fact, ct).ConfigureAwait(false);
                    return verified.IsSuccess
                        ? Check(request, verified.Value!.Facts.Operation, verified.Value.Source, verified.Value.Facts.MemberId)
                        : Result.Failure(verified.Error!);
                }
            default:
                return AdministratorReceiverSupport.InvalidResult("The source proof is not a withdrawal source fact.");
        }
    }

    static Result Check(CompleteAdministratorWithdrawal request, OperationProvenance operation,
        EventStreamAddress source, Uuid memberId) =>
        operation.RootOperationId == request.OperationId && operation.Episode == request.MemberEpisode.ToString() &&
        memberId == request.MemberId && source.Realm == request.Target.Realm
            ? Result.Success
            : AdministratorReceiverSupport.InvalidResult("The source fact does not carry this withdrawal operation.");
}
