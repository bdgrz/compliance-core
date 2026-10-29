using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Evaluations.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Queries;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Remediation.Readers;
namespace Bdgrz.Compliance.Features.Evaluations.Handlers;

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
