using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.ControlMappings.Services;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.ControlMappings.Queries;
namespace Bdgrz.Compliance.Features.ControlMappings.Handlers;

/// <summary>Lists a program's mappings from the caught-up mapping projection.</summary>
public sealed class ListControlCriterionMappingsHandler(CriteriaCoverageReadConsistency coverage)
    : IRequestHandler<ListControlCriterionMappings, Page<ControlCriterionMappingView>>
{
    public async ValueTask<Result<Page<ControlCriterionMappingView>>> HandleAsync(
        IRequestContext<ListControlCriterionMappings> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Status is not (null or "pending" or "active" or "rejected" or "retired"))
            return Result<Page<ControlCriterionMappingView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The mapping status filter must be pending, active, rejected, or retired."));
        var projected = await coverage.ReadMappingsAsync(request.TenantId, request.ProgramId, ct)
            .ConfigureAwait(false);
        if (!projected.IsSuccess)
            return Result<Page<ControlCriterionMappingView>>.Failure(projected.Error);
        var items = projected.Value.Where(mapping =>
                (request.ControlId is null || mapping.ControlId == request.ControlId) &&
                (request.EditionId is null || mapping.EditionId == request.EditionId) &&
                (request.Status is null || mapping.Status == request.Status))
            .ToArray();
        return ControlActivationSource.Paginate(items, request.Limit, request.Cursor,
            "control criterion mapping");
    }
}
