using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Records the acting member's own plan approval; HTTP-only. Approval re-verifies that the plan still targets the
///     control's current approved version and freezes the complete list of open work that moves to the approved
///     owner. An exact retry returns the original frozen view.
/// </summary>
public sealed class ApproveControlOperatingPlanHandler(NativeOperationExecutor operations,
    ControlOperationsReader operatingReader, IAggregateReader reader, IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<ApproveControlOperatingPlan, ControlOperatingPlanView>
{
    const string CommandKind = "ApproveControlOperatingPlan";

    public async ValueTask<Result<ControlOperatingPlanView>> HandleAsync(
        IRequestContext<ApproveControlOperatingPlan> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result<ControlOperatingPlanView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Operating plan approval requires personal HTTP submission."));
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.PlanVersionId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result<ControlOperatingPlanView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A plan approval requires a command id, a plan version and a non-negative expected revision."));
        var control = await ControlOperationsSource.LoadControlAsync(reader, request.TenantId, request.ProgramId,
            request.ControlId, ct).ConfigureAwait(false);
        if (control is null)
            return Result<ControlOperatingPlanView>.Failure(ControlOperationsSource.ControlNotFound());
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        var stream = new ControlOperatingPlan(request.TenantId, request.ProgramId, request.ControlId).Stream;
        var provenance = ControlOperationsSource.Provenance(request.CommandId, CommandKind, stream, actor,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var (current, line) = await operatingReader.ReadLineAsync(request.TenantId, request.ProgramId,
            request.ControlId, request.CommandId, ct).ConfigureAwait(false);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? reassignments = null;
        ImmutableContentRef? result = null;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            SeparationOfDutiesWaiver? waiver = null;
            if (request.SeparationOfDutiesWaiverId is { } waiverId)
                waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId, waiverId), ct)
                    .ConfigureAwait(false);
            var plan = line.Find(request.PlanVersionId);
            var failure = plan is not null && plan.Status == ControlOperationStates.PendingApproval &&
                          (control.IsRetired || control.ApprovedVersion?.VersionId != plan.ControlVersionId ||
                           control.ApprovedVersion.Status != ControlOperationStates.Approved)
                ? CommandFailure.StateConflict("The plan no longer targets the control's current approved version.")
                : ControlOperationRules.CheckApproval(line, request.TenantId, request.ExpectedRevision,
                    request.PlanVersionId, request.Rationale, actor.MemberId, now, waiver);
            if (failure is not null)
                return Result<ControlOperatingPlanView>.Failure(failure.ToRequestError());
            var snapshot = await operatingReader.ReadControlAsync(request.TenantId, request.ProgramId,
                request.ControlId, ct).ConfigureAwait(false);
            var moves = snapshot.OpenWorkToReassign(request.ControlId, plan!.Owner, request.PlanVersionId);
            var approved = plan with
            {
                Status = ControlOperationStates.Approved,
                Revision = request.ExpectedRevision + 1,
                ApprovedBy = ActorReference.ForMember(actor.MemberId, actor.Display),
                ApprovedAt = now,
                ApprovalRationale = request.Rationale.Trim(),
                ApprovalSeparationOfDutiesWaiverId = waiver?.Id,
                Reassignments = moves,
            };
            reassignments = await ControlOperatingContent.WriteAsync(content, request.TenantId,
                ControlOperatingContent.ReassignmentsSchema, moves, ct).ConfigureAwait(false);
            var body = await ControlOperatingContent.WriteAsync(content, request.TenantId,
                ControlOperatingContent.ApprovedPlanSchema, approved, ct).ConfigureAwait(false);
            result = await ControlOperatingContent.WriteResultAsync(content, request.TenantId,
                ControlOperatingContent.ApprovedPlanSchema, body, ct).ConfigureAwait(false);
            response = ControlOperatingContent.OkResponse(ControlOperatingContent.ApprovedPlanSchema, body);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<ControlOperatingPlan>(binding,
            () => new ControlOperatingPlan(request.TenantId, request.ProgramId, request.ControlId,
                request.CommandId), plan => plan.Acceptance,
            plan => Prepare(plan, request, response),
            (ControlOperatingPlan plan, ImmutableContentRef frozen) => plan.Approve(provenance,
                request.PlanVersionId, actor.MemberId, ActorReference.ForMember(actor.MemberId, actor.Display), now,
                request.Rationale.Trim(), request.SeparationOfDutiesWaiverId, reassignments!, frozen, result!)),
            context, ct).ConfigureAwait(false);
        return await ControlOperatingContent.ResponseAsync<ControlOperatingPlanView>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }

    static Result<FirstResponseDescriptor> Prepare(ControlOperatingPlan plan, ApproveControlOperatingPlan request,
        FirstResponseDescriptor? response)
    {
        var prepared = plan.PrepareApproval(request.ExpectedRevision, request.PlanVersionId);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The operating plan changed while the approval was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }
}
