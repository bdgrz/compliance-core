using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Reads the bounded runtime edition directory.</summary>
public sealed class ListCriteriaCatalogEditionsHandler(IRuntimeCriteriaCatalogReader catalog)
    : IRequestHandler<ListCriteriaCatalogEditions, IReadOnlyList<CriteriaCatalogEdition>>
{
    public ValueTask<Result<IReadOnlyList<CriteriaCatalogEdition>>> HandleAsync(
        IRequestContext<ListCriteriaCatalogEditions> context, CancellationToken ct) =>
        catalog.ListLegacyEditionsAsync(context.Request.TenantId, ct);
}
