using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Operations.Queries;
using Bdgrz.Compliance.Features.Operations.Readers;
namespace Bdgrz.Compliance.Features.Operations.Handlers;

public sealed class GetControlOperatingPlanHandler(ControlReader controlSources, ControlOperationsReader operations)
    : IRequestHandler<GetControlOperatingPlan, ControlOperatingPlanSetView>
{
    public ValueTask<Result<ControlOperatingPlanSetView>> HandleAsync(
        IRequestContext<GetControlOperatingPlan> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<ControlOperatingPlanSetView>> HandleCoreAsync(
        IRequestContext<GetControlOperatingPlan> context, CancellationToken ct)
    {
        var request = context.Request;
        if (await ControlOperationsSource.LoadControlAsync(controlSources, request.TenantId,
                request.ProgramId, request.ControlId, ct).ConfigureAwait(false) is null)
            return Result<ControlOperatingPlanSetView>.Failure(
                ControlOperationsSource.ControlNotFound());
        var snapshot = await operations.ReadControlAsync(request.TenantId, request.ProgramId, request.ControlId, ct)
            .ConfigureAwait(false);
        return Result<ControlOperatingPlanSetView>.Success(snapshot.ReadPlans(request.ControlId));
    }
}
