using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Evaluations.Services;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Queries;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Remediation.Readers;
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
