using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Reads one bounded page from the published-edition directory.</summary>
public sealed class ListRuntimeCriteriaEditionsHandler(IRuntimeCriteriaCatalogReader reader)
    : IRequestHandler<ListRuntimeCriteriaEditions, RuntimeCriteriaEditionsPage>
{
    public ValueTask<Result<RuntimeCriteriaEditionsPage>> HandleAsync(
        IRequestContext<ListRuntimeCriteriaEditions> context, CancellationToken ct)
    {
        var request = context.Request;
        return reader.ListEditionsAsync(request.TenantId, request.Limit, request.Cursor, ct);
    }
}
