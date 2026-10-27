using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Bdgrz.Compliance.Features.Criteria.Readers;
namespace Bdgrz.Compliance.Features.Criteria.Handlers;

public sealed class ListCriteriaCatalogEntriesHandler(ICriteriaCatalog catalog,
    ICriteriaTextOverlayReader overlays)
    : IRequestHandler<ListCriteriaCatalogEntries, Page<Criterion>>
{
    public ValueTask<Result<Page<Criterion>>> HandleAsync(
        IRequestContext<ListCriteriaCatalogEntries> context, CancellationToken ct)
    {
        var request = context.Request;
        return CriteriaCatalogEntriesPage.ReadAsync(catalog, overlays, request.TenantId,
            request.EditionId, request.Category, request.Kind, request.ParentIdentifier,
            request.Limit, request.Cursor, CriteriaTextOverlayPurpose.Display, ct);
    }
}
