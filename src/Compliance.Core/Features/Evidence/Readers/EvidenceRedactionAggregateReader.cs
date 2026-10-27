using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Finalizes the same complete lineage/source cut before explicit reads and executor-internal hydration.</summary>
public sealed class EvidenceRedactionAggregateReader(IAggregateReader inner, IDomainEventReader events,
    IImmutableContentStore content, ISourceFactResolver resolver) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        if (owner is EvidenceRedaction redaction)
            _ = await EvidenceRedactionHistory.ReadAsync(events, redaction, content, resolver, ct).ConfigureAwait(false);
        return owner;
    }
}
