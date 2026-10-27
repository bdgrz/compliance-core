using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Queries;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Handlers;

/// <summary>Lists an adopted assessment's gaps joined with the current plan read from each gap's own owner.</summary>
public sealed class ListReadinessGapsHandler(ReadinessReader readiness)
    : IRequestHandler<ListReadinessGaps, Page<ReadinessGapView>>
{
    public async ValueTask<Result<Page<ReadinessGapView>>> HandleAsync(
        IRequestContext<ListReadinessGaps> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.PlanState is not (null or "planned" or "unplanned"))
            return Result<Page<ReadinessGapView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The plan state filter must be planned or unplanned."));
        try
        {
            var view = await readiness.ReadViewAsync(request.TenantId, request.ProgramId, request.AssessmentId, ct)
                .ConfigureAwait(false);
            if (view is null)
                return Result<Page<ReadinessGapView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The readiness assessment was not found."));
            var items = view.Gaps.Where(gap =>
                (request.PlanState is null || (gap.Plan is not null) == (request.PlanState == "planned")) &&
                (request.OwnerMemberId is not { } owner || gap.Plan?.OwnerMemberId == owner) &&
                (request.Kind is null || gap.Kind == request.Kind) &&
                (request.RuleId is null || gap.RuleId == request.RuleId) &&
                (request.Subject is null || gap.Subject == request.Subject)).ToArray();
            return ControlActivationSource.Paginate(items, request.Limit, request.Cursor, "readiness gaps");
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<Page<ReadinessGapView>>.Failure(GetReadinessAssessmentHandler.Unverified(exception));
        }
    }
}
