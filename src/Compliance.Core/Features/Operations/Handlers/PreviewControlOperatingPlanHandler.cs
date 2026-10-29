using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Queries;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Operations.Handlers;

/// <summary>Shows what a proposed plan would schedule, conflict with, and reassign; records nothing.</summary>
public sealed class PreviewControlOperatingPlanHandler(ControlReader controlSources,
    ControlOperationsReader operations, OperatingAuthority authority)
    : IRequestHandler<PreviewControlOperatingPlan, ControlOperatingPlanPreview>
{
    const int UpcomingCount = 4;

    public ValueTask<Result<ControlOperatingPlanPreview>> HandleAsync(
        IRequestContext<PreviewControlOperatingPlan> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<ControlOperatingPlanPreview>> HandleCoreAsync(
        IRequestContext<PreviewControlOperatingPlan> context, CancellationToken ct)
    {
        var request = context.Request;
        var checkedPlan = await OperatingPlanChecks.CheckAsync(controlSources, authority, request.TenantId,
            request.ProgramId, request.ControlId, request.ControlVersionId, request.Owner,
            request.BackupOwner, request.ReviewerMemberId, ct).ConfigureAwait(false);
        if (!checkedPlan.IsSuccess)
            return Result<ControlOperatingPlanPreview>.Failure(checkedPlan.Error!);
        var (version, reviewerHoldsWork) = checkedPlan.Value;
        var ledger = await operations.ReadControlAsync(request.TenantId, request.ProgramId, request.ControlId, ct)
            .ConfigureAwait(false);
        if (ControlOperationRules.ValidatePlan(version, request.Owner, request.BackupOwner,
                request.ReviewerMemberId, request.Cadence, request.EffectiveFrom,
                ledger.CurrentPlan(request.ControlId)) is { } invalid)
            return Result<ControlOperatingPlanPreview>.Failure(new RequestError(
                RequestErrorKind.Validation, invalid.Message!));
        var upcoming = ControlCadenceSchedule.Periods(request.Cadence, request.EffectiveFrom,
                version.EffectiveUntil, request.EffectiveFrom.AddYears(2))
            .Take(UpcomingCount)
            .Select(period => new ControlOccurrenceView(request.TenantId, request.ProgramId,
                request.ControlId, ControlOccurrenceIdentity.Of(request.TenantId,
                    request.ProgramId, request.ControlId, ControlOccurrenceIdentity.WindowOf(period)), 0, ControlOperationStates.Expected,
                ControlOperationStates.Expected, version.VersionId, Uuid.Empty, period.Start,
                period.End, period.DueOn, null, request.Owner, [], [], []))
            .ToArray();
        string[] conflicts = reviewerHoldsWork
            ? ["self_review: the reviewer also holds the control's operating work and needs an approved SoD waiver."]
            : [];
        return Result<ControlOperatingPlanPreview>.Success(new ControlOperatingPlanPreview(
            ControlCadenceSchedule.Describe(request.Cadence),
            version.Content.ExpectedEvidenceDescriptions, upcoming, conflicts,
            ledger.OpenWorkToReassign(request.ControlId, request.Owner, Uuid.Empty)));
    }
}
