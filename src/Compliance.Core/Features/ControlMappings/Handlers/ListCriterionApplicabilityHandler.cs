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

/// <summary>Lists a program's applicability decisions from the caught-up projection.</summary>
public sealed class ListCriterionApplicabilityHandler(CriteriaCoverageReadConsistency coverage)
    : IRequestHandler<ListCriterionApplicability, Page<CriterionApplicabilityView>>
{
    public async ValueTask<Result<Page<CriterionApplicabilityView>>> HandleAsync(
        IRequestContext<ListCriterionApplicability> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Status is not (null or "pending" or "not_applicable" or "rejected" or
            "withdrawn"))
            return Result<Page<CriterionApplicabilityView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The applicability status filter must be pending, not_applicable, rejected, or withdrawn."));
        var projected = await coverage.ReadDecisionsAsync(request.TenantId, request.ProgramId,
            ct).ConfigureAwait(false);
        if (!projected.IsSuccess)
            return Result<Page<CriterionApplicabilityView>>.Failure(projected.Error);
        var items = projected.Value.Where(decision =>
                (request.EditionId is null || decision.EditionId == request.EditionId) &&
                (request.Status is null || decision.Status == request.Status))
            .ToArray();
        return ControlActivationSource.Paginate(items, request.Limit, request.Cursor,
            "criterion applicability");
    }
}
