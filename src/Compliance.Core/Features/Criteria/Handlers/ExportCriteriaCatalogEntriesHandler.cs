using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Readers;
namespace Bdgrz.Compliance.Features.Criteria.Handlers;

public sealed class ExportCriteriaCatalogEntriesHandler(ICriteriaCatalog catalog,
    ICriteriaTextOverlayReader overlays)
    : IRequestHandler<ExportCriteriaCatalogEntries, Page<Criterion>>
{
    public ValueTask<Result<Page<Criterion>>> HandleAsync(
        IRequestContext<ExportCriteriaCatalogEntries> context, CancellationToken ct)
    {
        var request = context.Request;
        return CriteriaCatalogEntriesPage.ReadAsync(catalog, overlays, request.TenantId,
            request.EditionId, request.Category, request.Kind, request.ParentIdentifier,
            request.Limit, request.Cursor, CriteriaTextOverlayPurpose.Export, ct);
    }
}
