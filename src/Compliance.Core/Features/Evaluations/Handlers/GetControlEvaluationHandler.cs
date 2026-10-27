using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>Reads one evaluation with its frozen procedure, every submission, and every review.</summary>
public sealed class GetControlEvaluationHandler(ControlReader controlSources, FindingReader findings,
    ControlEvaluationReader evaluations)
    : IRequestHandler<GetControlEvaluation, ControlEvaluationView>
{
    public ValueTask<Result<ControlEvaluationView>> HandleAsync(
        IRequestContext<GetControlEvaluation> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<ControlEvaluationView>> HandleCoreAsync(
        IRequestContext<GetControlEvaluation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (await ControlOperationsSource.LoadControlAsync(controlSources, request.TenantId,
                request.ProgramId, request.ControlId, ct).ConfigureAwait(false) is null)
            return Result<ControlEvaluationView>.Failure(ControlOperationsSource.ControlNotFound());
        if (await evaluations.ReadViewAsync(request.TenantId, request.ProgramId, request.ControlId,
                request.EvaluationId, ct).ConfigureAwait(false) is not { } view)
            return Result<ControlEvaluationView>.Failure(ControlEvaluationSource.EvaluationNotFound());
        var remediation = await findings.ReadAsync(request.TenantId,
            request.ProgramId, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        if (!remediation.IsSuccess)
            return Result<ControlEvaluationView>.Failure(remediation.Error);
        return Result<ControlEvaluationView>.Success(
            ControlEvaluationSource.WithRouting(view, remediation.Value));
    }
}
