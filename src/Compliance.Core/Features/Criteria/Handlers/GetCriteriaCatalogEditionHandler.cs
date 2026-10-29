using System.Globalization;
using System.Text;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
namespace Bdgrz.Compliance.Features.Criteria.Handlers;

public sealed class GetCriteriaCatalogEditionHandler(ICriteriaCatalog catalog)
    : IRequestHandler<GetCriteriaCatalogEdition, CriteriaCatalogEdition>
{
    public ValueTask<Result<CriteriaCatalogEdition>> HandleAsync(
        IRequestContext<GetCriteriaCatalogEdition> context, CancellationToken ct)
    {
        var edition = catalog.GetEdition(context.Request.EditionId);
        return ValueTask.FromResult(edition is null
            ? Result<CriteriaCatalogEdition>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The criteria edition was not found."))
            : Result<CriteriaCatalogEdition>.Success(edition));
    }
}
