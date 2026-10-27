using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Queries;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations.Handlers;

/// <summary>Lists a control's evaluations, newest first, keeping every prior decision visible.</summary>
public sealed class ListControlEvaluationsHandler(ControlReader controlSources, FindingReader findings,
    ControlEvaluationReader evaluations)
    : IRequestHandler<ListControlEvaluations, Page<ControlEvaluationView>>
{
    public ValueTask<Result<Page<ControlEvaluationView>>> HandleAsync(
        IRequestContext<ListControlEvaluations> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<Page<ControlEvaluationView>>> HandleCoreAsync(
        IRequestContext<ListControlEvaluations> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.State is not (null or ControlEvaluationStates.InProgress or
            ControlEvaluationStates.Submitted or ControlEvaluationStates.Accepted))
            return Result<Page<ControlEvaluationView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The state filter must be in_progress, submitted, or accepted."));
        var control = await ControlOperationsSource.LoadControlAsync(controlSources, request.TenantId,
            request.ProgramId, request.ControlId, ct).ConfigureAwait(false);
        if (control is null)
            return Result<Page<ControlEvaluationView>>.Failure(
                ControlOperationsSource.ControlNotFound());
        var controlEvaluations = await evaluations.ReadControlAsync(request.TenantId, request.ProgramId,
            request.ControlId, ct).ConfigureAwait(false);
        var remediation = await findings.ReadAsync(request.TenantId,
            request.ProgramId, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        if (!remediation.IsSuccess)
            return Result<Page<ControlEvaluationView>>.Failure(remediation.Error);
        var items = controlEvaluations
            .Where(view => request.State is null || view.State == request.State)
            .Select(view => ControlEvaluationSource.WithRouting(view, remediation.Value)).ToArray();
        return ControlActivationSource.Paginate(items, request.Limit, request.Cursor,
            "control evaluations");
    }
}
