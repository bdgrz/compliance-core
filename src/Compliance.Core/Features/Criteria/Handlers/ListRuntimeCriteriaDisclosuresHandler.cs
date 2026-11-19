using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Reads one source-qualified page of edition disclosures.</summary>
public sealed class ListRuntimeCriteriaDisclosuresHandler(IRuntimeCriteriaCatalogReader reader)
    : IRequestHandler<ListRuntimeCriteriaDisclosures, RuntimeCriteriaDisclosurePage>
{
    public ValueTask<Result<RuntimeCriteriaDisclosurePage>> HandleAsync(
        IRequestContext<ListRuntimeCriteriaDisclosures> context, CancellationToken ct)
    {
        var request = context.Request;
        return reader.ListDisclosuresAsync(request.TenantId, request.EditionId, request.Limit,
            request.Cursor, ct);
    }
}
