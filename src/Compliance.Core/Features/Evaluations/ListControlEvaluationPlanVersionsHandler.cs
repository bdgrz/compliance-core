using Bdgrz.Compliance.Features.Content;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

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
