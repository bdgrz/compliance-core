using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Queries;
using Bdgrz.Compliance.Features.Evaluations.Readers;
namespace Bdgrz.Compliance.Features.Evaluations.Handlers;

public sealed class GetControlEvaluationPlanVersionHandler(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content, ControlReader controlSources)
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
        var versions = await ControlEvaluationPlanReader.ReadAsync(reader, events, content, request.TenantId,
            request.ProgramId, request.ControlId, ct).ConfigureAwait(false);
        return versions.FirstOrDefault(version => version.PlanVersionId == request.PlanVersionId) is { } version
            ? Result<ControlEvaluationPlanVersionView>.Success(version)
            : Result<ControlEvaluationPlanVersionView>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The control evaluation plan version was not found."));
    }
}
