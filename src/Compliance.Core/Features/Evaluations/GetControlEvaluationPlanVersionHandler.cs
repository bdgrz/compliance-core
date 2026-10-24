using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

public sealed class GetControlEvaluationPlanVersionHandler(IAggregateReader reader, ControlReader controlSources)
    : IRequestHandler<GetControlEvaluationPlanVersion, ControlEvaluationPlanVersionView>
{
    public ValueTask<Result<ControlEvaluationPlanVersionView>> HandleAsync(
        IRequestContext<GetControlEvaluationPlanVersion> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<ControlEvaluationPlanVersionView>> HandleCoreAsync(
        IRequestContext<GetControlEvaluationPlanVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        if (await ControlOperationsSource.LoadControlAsync(controlSources, request.TenantId,
                request.ProgramId, request.ControlId, ct).ConfigureAwait(false) is null)
            return Result<ControlEvaluationPlanVersionView>.Failure(
                ControlOperationsSource.ControlNotFound());
        var plan = await reader.HydrateAsync(new ControlEvaluationPlan(request.TenantId,
            request.ProgramId, request.ControlId), ct).ConfigureAwait(false);
        return plan.FindVersion(request.PlanVersionId) is { } version
            ? Result<ControlEvaluationPlanVersionView>.Success(version)
            : Result<ControlEvaluationPlanVersionView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The control evaluation plan version was not found."));
    }
}
