using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

public sealed class GetControlOperatingPlanHandler(IAggregateReader reader, ControlOperationsReader operations)
    : IRequestHandler<GetControlOperatingPlan, ControlOperatingPlanSetView>
{
    public async ValueTask<Result<ControlOperatingPlanSetView>> HandleAsync(
        IRequestContext<GetControlOperatingPlan> context, CancellationToken ct)
    {
        var request = context.Request;
        if (await ControlOperationsSource.LoadControlAsync(reader, request.TenantId,
                request.ProgramId, request.ControlId, ct).ConfigureAwait(false) is null)
            return Result<ControlOperatingPlanSetView>.Failure(
                ControlOperationsSource.ControlNotFound());
        var snapshot = await operations.ReadControlAsync(request.TenantId, request.ProgramId, request.ControlId, ct)
            .ConfigureAwait(false);
        return Result<ControlOperatingPlanSetView>.Success(snapshot.ReadPlans(request.ControlId));
    }
}
