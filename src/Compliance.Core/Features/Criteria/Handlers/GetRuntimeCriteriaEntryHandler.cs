using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Queries;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>Reads one criterion from the runtime source-cut directory.</summary>
public sealed class GetRuntimeCriteriaEntryHandler(IRuntimeCriteriaCatalogReader reader)
    : IRequestHandler<GetRuntimeCriteriaEntry, RuntimeCriteriaEntry>
{
    public ValueTask<Result<RuntimeCriteriaEntry>> HandleAsync(
        IRequestContext<GetRuntimeCriteriaEntry> context, CancellationToken ct) =>
        reader.GetEntryAsync(context.Request.EditionId, context.Request.Identifier, ct);
}
