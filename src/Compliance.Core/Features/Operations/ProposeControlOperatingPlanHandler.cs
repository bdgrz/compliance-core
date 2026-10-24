using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Proposes a plan for the control's exact current approved version after verifying every holder against source
///     records and checking reviewer independence. The immutable plan version id is the logical operation identity,
///     so an exact retry returns the original frozen view even after later changes to the line.
/// </summary>
public sealed class ProposeControlOperatingPlanHandler(NativeOperationExecutor operations,
    ControlOperationsReader operatingReader, IAggregateReader reader, ControlReader controlSources, OperatingAuthority authority,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<ProposeControlOperatingPlan, ControlOperatingPlanView>
{
    const string CommandKind = "ProposeControlOperatingPlan";

    public ValueTask<Result<ControlOperatingPlanView>> HandleAsync(
        IRequestContext<ProposeControlOperatingPlan> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<ControlOperatingPlanView>> HandleCoreAsync(
        IRequestContext<ProposeControlOperatingPlan> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.PlanVersionId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result<ControlOperatingPlanView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A plan proposal requires a plan version id and a non-negative expected revision."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        var stream = new ControlOperatingPlan(request.TenantId, request.ProgramId, request.ControlId).Stream;
        var provenance = ControlOperationsSource.Provenance(request.PlanVersionId, CommandKind, stream, actor,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var (current, line) = await operatingReader.ReadLineAsync(request.TenantId, request.ProgramId,
            request.ControlId, request.PlanVersionId, ct).ConfigureAwait(false);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? planContent = null;
        ImmutableContentRef? result = null;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var checkedPlan = await OperatingPlanChecks.CheckAsync(controlSources, authority, request.TenantId,
                request.ProgramId, request.ControlId, request.ControlVersionId, request.Owner, request.BackupOwner,
                request.ReviewerMemberId, ct).ConfigureAwait(false);
            if (!checkedPlan.IsSuccess)
                return Result<ControlOperatingPlanView>.Failure(checkedPlan.Error!);
            var (version, reviewerHoldsWork) = checkedPlan.Value;
            SeparationOfDutiesWaiver? waiver = null;
            if (request.SeparationOfDutiesWaiverId is { } waiverId)
                waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId, waiverId), ct)
                    .ConfigureAwait(false);
            var failure = ControlOperationRules.CheckProposal(line, request.TenantId, request.ExpectedRevision,
                version, request.Owner, request.BackupOwner, request.ReviewerMemberId, reviewerHoldsWork,
                request.Cadence, request.EffectiveFrom, request.Rationale, now, waiver);
            if (failure is not null)
                return Result<ControlOperatingPlanView>.Failure(failure.ToRequestError());
            var clean = ControlCadenceSchedule.Clean(request.Cadence);
            var view = new ControlOperatingPlanView(request.TenantId, request.ProgramId, request.ControlId,
                request.PlanVersionId, request.ExpectedRevision + 1, ControlOperationStates.PendingApproval,
                version.VersionId, request.Owner, request.BackupOwner, request.ReviewerMemberId, clean,
                ControlCadenceSchedule.Describe(clean), version.Content.ExpectedEvidenceDescriptions.ToArray(),
                request.EffectiveFrom, null, request.Rationale.Trim(), actor.MemberId,
                ActorReference.ForMember(actor.MemberId, actor.Display), now, waiver?.Id);
            planContent = await ControlOperatingContent.WriteAsync(content, request.TenantId,
                ControlOperatingContent.PlanSchema, view, ct).ConfigureAwait(false);
            result = await ControlOperatingContent.WriteResultAsync(content, request.TenantId,
                ControlOperatingContent.PlanSchema, planContent, ct).ConfigureAwait(false);
            response = ControlOperatingContent.OkResponse(ControlOperatingContent.PlanSchema, planContent);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<ControlOperatingPlan>(binding,
            () => new ControlOperatingPlan(request.TenantId, request.ProgramId, request.ControlId,
                request.PlanVersionId), plan => plan.Acceptance,
            plan => Prepare(plan, request.ExpectedRevision, response),
            (ControlOperatingPlan plan, ImmutableContentRef frozen) =>
                plan.Propose(provenance, planContent!, frozen, result!)), context, ct).ConfigureAwait(false);
        return await ControlOperatingContent.ResponseAsync<ControlOperatingPlanView>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }

    static Result<FirstResponseDescriptor> Prepare(ControlOperatingPlan plan, long expectedRevision,
        FirstResponseDescriptor? response)
    {
        var prepared = plan.PrepareProposal(expectedRevision);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The operating plan changed while the proposal was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }
}
