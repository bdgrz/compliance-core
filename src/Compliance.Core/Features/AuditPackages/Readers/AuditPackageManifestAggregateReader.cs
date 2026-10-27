using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Readers;

/// <summary>Finalizes complete raw native authority for both queries and executor-internal hydration.</summary>
public sealed class AuditPackageManifestAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        var hydrated = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (hydrated is AuditPackageManifest owner)
            _ = await AuditPackageManifestHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
        return hydrated;
    }
}
