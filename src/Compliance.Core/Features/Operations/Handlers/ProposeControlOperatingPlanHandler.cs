using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Operations.Handlers;

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

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? planContent = null;
        ImmutableContentRef? result = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<ControlOperatingPlan>(binding,
            () => new ControlOperatingPlan(request.TenantId, request.ProgramId, request.ControlId,
                request.PlanVersionId), plan => plan.Acceptance,
            async (current, token) =>
            {
                try
                {
                    var line = await operatingReader.ComposeLineAsync(current, token).ConfigureAwait(false);
                    var checkedPlan = await OperatingPlanChecks.CheckAsync(controlSources, authority, request.TenantId,
                        request.ProgramId, request.ControlId, request.ControlVersionId, request.Owner, request.BackupOwner,
                        request.ReviewerMemberId, token).ConfigureAwait(false);
                    if (!checkedPlan.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(checkedPlan.Error!);
                    var (version, reviewerHoldsWork) = checkedPlan.Value;
                    SeparationOfDutiesWaiver? waiver = null;
                    if (request.SeparationOfDutiesWaiverId is { } waiverId)
                        waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId, waiverId), token)
                            .ConfigureAwait(false);
                    var failure = ControlOperationRules.CheckProposal(line, request.TenantId, request.ExpectedRevision,
                        version, request.Owner, request.BackupOwner, request.ReviewerMemberId, reviewerHoldsWork,
                        request.Cadence, request.EffectiveFrom, request.Rationale, now, waiver);
                    if (failure is not null)
                        return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
                    var clean = ControlCadenceSchedule.Clean(request.Cadence);
                    var view = new ControlOperatingPlanView(request.TenantId, request.ProgramId, request.ControlId,
                        request.PlanVersionId, request.ExpectedRevision + 1, ControlOperationStates.PendingApproval,
                        version.VersionId, request.Owner, request.BackupOwner, request.ReviewerMemberId, clean,
                        ControlCadenceSchedule.Describe(clean), version.Content.ExpectedEvidenceDescriptions.ToArray(),
                        request.EffectiveFrom, null, request.Rationale.Trim(), actor.MemberId,
                        ActorReference.ForMember(actor.MemberId, actor.Display), now, waiver?.Id);
                    planContent = await ControlOperatingContent.WriteAsync(content, request.TenantId,
                        ControlOperatingContent.PlanSchema, view, token).ConfigureAwait(false);
                    result = await ControlOperatingContent.WriteResultAsync(content, request.TenantId,
                        ControlOperatingContent.PlanSchema, planContent, token).ConfigureAwait(false);
                    response = ControlOperatingContent.OkResponse(ControlOperatingContent.PlanSchema, planContent);
                    return Result<FirstResponseDescriptor>.Success(response!);
                }
                catch (ImmutableContentIntegrityException failure)
                {
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The exact operating source content cannot be verified.", failure is ImmutableContentUnavailableException));
                }
            },
            (ControlOperatingPlan plan, ImmutableContentRef frozen) =>
                plan.Propose(provenance, planContent!, frozen, result!)), context, ct).ConfigureAwait(false);
        return await ControlOperatingContent.ResponseAsync<ControlOperatingPlanView>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }

}
