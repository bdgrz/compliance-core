using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Reads edition details from the runtime projection.</summary>
public sealed class GetCriteriaCatalogEditionHandler(IRuntimeCriteriaCatalogReader catalog)
    : IRequestHandler<GetCriteriaCatalogEdition, CriteriaCatalogEdition>
{
    public ValueTask<Result<CriteriaCatalogEdition>> HandleAsync(
        IRequestContext<GetCriteriaCatalogEdition> context, CancellationToken ct) =>
        catalog.GetLegacyEditionAsync(context.Request.TenantId, context.Request.EditionId, ct);
}
