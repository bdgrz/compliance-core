using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Operations.Handlers;

/// <summary>
///     Records the acting member's own plan approval; HTTP-only. Approval re-verifies that the plan still targets the
///     control's current approved version and freezes the complete list of open work that moves to the approved
///     owner. An exact retry returns the original frozen view.
/// </summary>
public sealed class ApproveControlOperatingPlanHandler(NativeOperationExecutor operations,
    ControlOperationsReader operatingReader, IAggregateReader reader, ControlReader controlSources, IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<ApproveControlOperatingPlan, ControlOperatingPlanView>
{
    const string CommandKind = "ApproveControlOperatingPlan";

    public ValueTask<Result<ControlOperatingPlanView>> HandleAsync(
        IRequestContext<ApproveControlOperatingPlan> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<ControlOperatingPlanView>> HandleCoreAsync(
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
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        var stream = new ControlOperatingPlan(request.TenantId, request.ProgramId, request.ControlId).Stream;
        var provenance = ControlOperationsSource.Provenance(request.CommandId, CommandKind, stream, actor,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? reassignments = null;
        ImmutableContentRef? result = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<ControlOperatingPlan>(binding,
            () => new ControlOperatingPlan(request.TenantId, request.ProgramId, request.ControlId,
                request.CommandId), plan => plan.Acceptance,
            async (current, token) =>
            {
                try
                {
                    var line = await operatingReader.ComposeLineAsync(current, token).ConfigureAwait(false);
                    var control = await ControlOperationsSource.LoadControlAsync(controlSources, request.TenantId, request.ProgramId,
                        request.ControlId, token).ConfigureAwait(false);
                    if (control is null)
                        return Result<FirstResponseDescriptor>.Failure(ControlOperationsSource.ControlNotFound());
                    SeparationOfDutiesWaiver? waiver = null;
                    if (request.SeparationOfDutiesWaiverId is { } waiverId)
                        waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId, waiverId), token)
                            .ConfigureAwait(false);
                    var plan = line.Find(request.PlanVersionId);
                    var failure = plan is not null && plan.Status == ControlOperationStates.PendingApproval &&
                                  (control.IsRetired || control.ApprovedVersion?.VersionId != plan.ControlVersionId ||
                                   control.ApprovedVersion.Status != ControlOperationStates.Approved)
                        ? CommandFailure.StateConflict("The plan no longer targets the control's current approved version.")
                        : ControlOperationRules.CheckApproval(line, request.TenantId, request.ExpectedRevision,
                            request.PlanVersionId, request.Rationale, actor.MemberId, now, waiver);
                    if (failure is not null)
                        return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
                    var snapshot = await operatingReader.ReadControlAsync(request.TenantId, request.ProgramId,
                        request.ControlId, token).ConfigureAwait(false);
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
                        ControlOperatingContent.ReassignmentsSchema, moves, token).ConfigureAwait(false);
                    var body = await ControlOperatingContent.WriteAsync(content, request.TenantId,
                        ControlOperatingContent.ApprovedPlanSchema, approved, token).ConfigureAwait(false);
                    result = await ControlOperatingContent.WriteResultAsync(content, request.TenantId,
                        ControlOperatingContent.ApprovedPlanSchema, body, token).ConfigureAwait(false);
                    response = ControlOperatingContent.OkResponse(ControlOperatingContent.ApprovedPlanSchema, body);
                    return Result<FirstResponseDescriptor>.Success(response!);
                }
                catch (ImmutableContentIntegrityException failure)
                {
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The exact operating source content cannot be verified.", failure is ImmutableContentUnavailableException));
                }
            },
            (ControlOperatingPlan plan, ImmutableContentRef frozen) => plan.Approve(provenance,
                request.PlanVersionId, actor.MemberId, ActorReference.ForMember(actor.MemberId, actor.Display), now,
                request.Rationale.Trim(), request.SeparationOfDutiesWaiverId, reassignments!, frozen, result!)),
            context, ct).ConfigureAwait(false);
        return await ControlOperatingContent.ResponseAsync<ControlOperatingPlanView>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }

}
