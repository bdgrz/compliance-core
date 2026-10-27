using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     Settles a restoration or first registration on the guard once the source fact carrying its exact operation
///     was applied. The matching reason alone is cleared; availability returns only when no other reason remains and
///     the member and the administrators-team assignment are read as currently eligible for the same episode. Any
///     other outcome completes the restoration without making the member available.
/// </summary>
public sealed class CompleteAdministratorRestorationHandler(AdministratorReceiverSupport support)
    : IRequestHandler<CompleteAdministratorRestoration, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<CompleteAdministratorRestoration> context, CancellationToken ct)
    {
        var request = context.Request;
        var source = await VerifySourceAsync(request, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<ReceiverOutcome>.Failure(source.Error!);
        var intent = await support.ReadIntentAsync(request, request.MemberEpisode, ct).ConfigureAwait(false);
        if (!intent.IsSuccess)
            return Result<ReceiverOutcome>.Failure(intent.Error!);
        var proofs = await ReadProofsAsync(request, ct).ConfigureAwait(false);
        if (!proofs.IsSuccess || proofs.Value!.Restoration.EventId != request.Source.Proof.EventId)
            return AdministratorReceiverSupport.Invalid<ReceiverOutcome>("The eligibility proofs name another fact.");
        var tenantId = AdministratorReceiverSupport.TenantOf(request.Target);
        var loaded = await support.Guards.LoadAsync(tenantId, request.CommandId, request.MemberEpisode, request.OperationId, ct).ConfigureAwait(false);
        var guard = loaded.Guard;
        if (request.Target != guard.Stream)
            return AdministratorReceiverSupport.Invalid<ReceiverOutcome>("The command does not target this tenant's guard.");

        var member = await support.Reader.HydrateAsync(Member.ForVerification(tenantId, request.MemberId), ct)
            .ConfigureAwait(false);
        var assignment = await support.Reader.HydrateAsync(new TeamMember(tenantId,
            BuiltInRbac.AdministratorsTeamId(tenantId), request.MemberId), ct).ConfigureAwait(false);
        var eligible = member.IsRegistered && !member.IsSuspended && member.MembershipEpisodeId == request.MemberEpisode &&
                       assignment.IsAssigned && assignment.MembershipEpisodeId == request.MemberEpisode &&
                       (intent.Value!.TransitionKind == AdministratorTransitionKind.ReinstateMember ||
                        intent.Value.AssignmentGeneration == assignment.AssignmentGeneration);
        var provenance = AdministratorReceiverSupport.Provenance(request, nameof(CompleteAdministratorRestoration),
            guard.Stream, intent.Value!, request.Source.Proof.EventId);
        var result = await AdministratorTransitionEffects.UnitResultAsync(support.Content, request.Target.Realm,
            request.OperationId, ct).ConfigureAwait(false);
        return await support.ApplyAsync(loaded.Create,
            current => current.Acceptance, AcceptedOperationBinding.Of(provenance),
            current => current.PendingGeneration(request.OperationId) == request.Generation
                ? null
                : AdministratorReceiverSupport.Invalid<ReceiverOutcome>("The guard has no such unfinished restoration."),
            (TenantManagerGuard current) => current.CompleteRestoration(provenance, request.OperationId, request.MemberId, eligible,
                intent.Value.Header.AffectedOwner,
                intent.Value.Header.OriginalExpectedRevision is { } revision ? (long)revision : -1, request.Intent,
                request.Source.Proof.RequiredFacts, result), context, ct).ConfigureAwait(false);
    }

    async Task<Result<AdministratorEligibilityProofs>> ReadProofsAsync(CompleteAdministratorRestoration request,
        CancellationToken ct)
    {
        try
        {
            return Result<AdministratorEligibilityProofs>.Success(
                await support.Content.ReadJsonAsync<AdministratorEligibilityProofs>(request.Target.Realm,
                    request.EligibilityProofs, ct).ConfigureAwait(false));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return AdministratorReceiverSupport.Invalid<AdministratorEligibilityProofs>(
                $"The eligibility proofs do not verify: {exception.Message}");
        }
    }

    async Task<Result> VerifySourceAsync(CompleteAdministratorRestoration request, CancellationToken ct)
    {
        switch (request.Source)
        {
            case CompleteAdministratorRestorationSourceProof.ReinstatedProof reinstated:
                {
                    var verified = await support.VerifySourceAsync(reinstated.Fact, ct).ConfigureAwait(false);
                    return verified.IsSuccess
                        ? Check(request, verified.Value!.Facts.Operation, verified.Value.Source, verified.Value.Facts.MemberId)
                        : Result.Failure(verified.Error!);
                }
            case CompleteAdministratorRestorationSourceProof.AssignedProof assigned:
                {
                    var verified = await support.VerifySourceAsync(assigned.Fact, ct).ConfigureAwait(false);
                    return verified.IsSuccess
                        ? Check(request, verified.Value!.Facts.Operation, verified.Value.Source, verified.Value.Facts.MemberId)
                        : Result.Failure(verified.Error!);
                }
            default:
                return AdministratorReceiverSupport.InvalidResult("The source proof is not a restoration source fact.");
        }
    }

    static Result Check(CompleteAdministratorRestoration request, OperationProvenance operation,
        EventStreamAddress source, Uuid memberId) =>
        operation.RootOperationId == request.OperationId && operation.Episode == request.MemberEpisode.ToString() &&
        memberId == request.MemberId && source.Realm == request.Target.Realm
            ? Result.Success
            : AdministratorReceiverSupport.InvalidResult("The source fact does not carry this restoration operation.");
}
