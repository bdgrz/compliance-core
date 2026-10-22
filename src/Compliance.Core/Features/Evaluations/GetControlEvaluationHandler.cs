using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>Reads one evaluation with its frozen procedure, every submission, and every review.</summary>
public sealed class GetControlEvaluationHandler(IAggregateReader reader, IDomainEventReader events)
    : IRequestHandler<GetControlEvaluation, ControlEvaluationView>
{
    public async ValueTask<Result<ControlEvaluationView>> HandleAsync(
        IRequestContext<GetControlEvaluation> context, CancellationToken ct)
    {
        var request = context.Request;
        if (await ControlOperationsSource.LoadControlAsync(reader, request.TenantId,
                request.ProgramId, request.ControlId, ct).ConfigureAwait(false) is null)
            return Result<ControlEvaluationView>.Failure(ControlOperationsSource.ControlNotFound());
        var ledger = await reader.HydrateAsync(new ControlEvaluationLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (ledger.Read(request.ControlId, request.EvaluationId) is not { } view)
            return Result<ControlEvaluationView>.Failure(ControlEvaluationSource.EvaluationNotFound());
        var remediation = await new FindingReadSource(reader, events).ReadAsync(request.TenantId,
            request.ProgramId, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        if (!remediation.IsSuccess)
            return Result<ControlEvaluationView>.Failure(remediation.Error);
        return Result<ControlEvaluationView>.Success(
            ControlEvaluationSource.WithRouting(view, remediation.Value));
    }
}
