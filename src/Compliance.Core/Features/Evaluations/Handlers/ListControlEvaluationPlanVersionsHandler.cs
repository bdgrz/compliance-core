using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Queries;
using Bdgrz.Compliance.Features.Evaluations.Readers;
namespace Bdgrz.Compliance.Features.Evaluations.Handlers;

public sealed class ListControlEvaluationPlanVersionsHandler(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content, ControlReader controlSources)
    : IRequestHandler<ListControlEvaluationPlanVersions, Page<ControlEvaluationPlanVersionView>>
{
    public ValueTask<Result<Page<ControlEvaluationPlanVersionView>>> HandleAsync(
        IRequestContext<ListControlEvaluationPlanVersions> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<Page<ControlEvaluationPlanVersionView>>> HandleCoreAsync(
        IRequestContext<ListControlEvaluationPlanVersions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ControlEvaluationPlanVersionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The page limit must be from 1 to 200."));
        if (await ControlOperationsSource.LoadControlAsync(controlSources, request.TenantId,
                request.ProgramId, request.ControlId, ct).ConfigureAwait(false) is null)
            return Result<Page<ControlEvaluationPlanVersionView>>.Failure(
                ControlOperationsSource.ControlNotFound());
        var versions = await ControlEvaluationPlanReader.ReadAsync(reader, events, content, request.TenantId,
            request.ProgramId, request.ControlId, ct).ConfigureAwait(false);
        return ControlActivationSource.Paginate(versions, request.Limit,
            request.Cursor, "control evaluation plan versions");
    }
}
