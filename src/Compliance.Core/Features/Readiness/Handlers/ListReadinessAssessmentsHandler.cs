using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Queries;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Handlers;

/// <summary>Lists a program's adopted assessments, newest first.</summary>
public sealed class ListReadinessAssessmentsHandler(ReadinessReader readiness)
    : IRequestHandler<ListReadinessAssessments, Page<ReadinessAssessmentSummaryView>>
{
    public async ValueTask<Result<Page<ReadinessAssessmentSummaryView>>> HandleAsync(
        IRequestContext<ListReadinessAssessments> context, CancellationToken ct)
    {
        var request = context.Request;
        try
        {
            var program = await readiness.ReadProgramAsync(request.TenantId, request.ProgramId, ct).ConfigureAwait(false);
            var summaries = await readiness.ReadSummariesAsync(request.TenantId, program, ct).ConfigureAwait(false);
            return ControlActivationSource.Paginate(summaries, request.Limit, request.Cursor, "readiness assessments");
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<Page<ReadinessAssessmentSummaryView>>.Failure(GetReadinessAssessmentHandler.Unverified(exception));
        }
    }
}
