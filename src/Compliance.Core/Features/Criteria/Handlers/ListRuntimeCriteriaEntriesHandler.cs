using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Reads one stable runtime-criteria page without materializing the catalog.</summary>
public sealed class ListRuntimeCriteriaEntriesHandler(IRuntimeCriteriaCatalogReader reader)
    : IRequestHandler<ListRuntimeCriteriaEntries, RuntimeCriteriaPage>
{
    public ValueTask<Result<RuntimeCriteriaPage>> HandleAsync(
        IRequestContext<ListRuntimeCriteriaEntries> context, CancellationToken ct)
    {
        var request = context.Request;
        return reader.ListEntriesAsync(request.TenantId, request.EditionId, request.Category, request.Kind,
            request.ParentIdentifier, request.Limit, request.Cursor, ct);
    }
}
