using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Reads a runtime edition using its source-backed directory.</summary>
public sealed class GetRuntimeCriteriaEditionHandler(IRuntimeCriteriaCatalogReader reader)
    : IRequestHandler<GetRuntimeCriteriaEdition, RuntimeCriteriaEdition>
{
    public ValueTask<Result<RuntimeCriteriaEdition>> HandleAsync(
        IRequestContext<GetRuntimeCriteriaEdition> context, CancellationToken ct) =>
        reader.GetEditionAsync(context.Request.EditionId, ct);
}
