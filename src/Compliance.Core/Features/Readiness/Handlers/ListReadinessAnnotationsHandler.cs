using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Queries;
using Bdgrz.Compliance.Features.Readiness.Readers;
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
