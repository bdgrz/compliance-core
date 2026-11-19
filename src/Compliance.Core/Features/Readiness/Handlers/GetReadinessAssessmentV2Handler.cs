using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Queries;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Handlers;

public sealed class GetReadinessAssessmentV2Handler(ReadinessAssessmentRecordReader assessments)
    : IRequestHandler<GetReadinessAssessmentV2, ReadinessAssessmentResult>
{
    public async ValueTask<Result<ReadinessAssessmentResult>> HandleAsync(
        IRequestContext<GetReadinessAssessmentV2> context, CancellationToken ct) =>
        await assessments.ReadAsync(context.Request.TenantId, context.Request.ProgramId,
            context.Request.AssessmentId, ct).ConfigureAwait(false) is { } view
            ? Result<ReadinessAssessmentResult>.Success(view)
            : Result<ReadinessAssessmentResult>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The readiness assessment was not found."));
}
