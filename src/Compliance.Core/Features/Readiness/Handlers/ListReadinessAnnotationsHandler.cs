using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Queries;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Handlers;

/// <summary>Lists advisor feedback on one adopted assessment from the gap plan owners.</summary>
public sealed class ListReadinessAnnotationsHandler(ReadinessReader readiness)
    : IRequestHandler<ListReadinessAnnotations, Page<ReadinessAnnotationView>>
{
    public async ValueTask<Result<Page<ReadinessAnnotationView>>> HandleAsync(
        IRequestContext<ListReadinessAnnotations> context, CancellationToken ct)
    {
        var request = context.Request;
        try
        {
            var program = await readiness.ReadProgramAsync(request.TenantId, request.ProgramId, ct).ConfigureAwait(false);
            return await readiness.ReadAnnotationsAsync(request.TenantId, program, request.AssessmentId, ct)
                .ConfigureAwait(false) is { } annotations
                ? ControlActivationSource.Paginate(annotations, request.Limit, request.Cursor, "readiness annotations")
                : Result<Page<ReadinessAnnotationView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The readiness assessment was not found."));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<Page<ReadinessAnnotationView>>.Failure(GetReadinessAssessmentHandler.Unverified(exception));
        }
    }
}
