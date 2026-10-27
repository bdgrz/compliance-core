using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceUse.Readers;

/// <summary>
/// Finalizes source-use owners at the exact hydrated cut. Full transient validation protects unselected historical
/// fences as well as selected ones. Its inner reader is the original keyed registration, never this decorator.
/// </summary>
public sealed class SourceUseAggregateReader(IAggregateReader inner, IDomainEventReader events) : IAggregateReader
{
    /// <inheritdoc />
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
        where TAggregate : Aggregate
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (aggregate is not ISourceUseOwner source)
            return await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        source.Uses.BeginHydration();
        var owner = await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        var history = new SourceUseHistoryValidation(owner.Stream);
        if (owner.Version == 0 && owner.CommittedStreamPosition == 0)
        {
            source.Uses.Confirm(history, 0, 0);
            return owner;
        }
        var position = 0UL;
        var version = 0UL;
        var ids = new HashSet<Uuid>();
        await foreach (var record in events.ReadAsync(owner.Stream, 0, ct).ConfigureAwait(false))
        {
            if (record.ResourceOffset >= owner.CommittedStreamPosition)
                break;
            var metadata = record.Event.Metadata;
            if (record.Stream != owner.Stream || record.ResourceOffset != position || metadata.IsAudit ||
                metadata.AggregateId != owner.Id || metadata.AggregateVersion != checked(version + 1) ||
                metadata.EventId == Uuid.Empty || !ids.Add(metadata.EventId))
                throw new ImmutableContentIntegrityException("The source-use history has another exact owner, physical offset or metadata version.");
            history.ObserveOwnerFact(record.Event);
            if (SourceUseFactKinds.Classify(record.Event) is { } kind)
                history.Observe(kind.Purpose, kind.Granted, (ISourceUseFact)record.Event);
            position = checked(record.ResourceOffset + 1);
            version = metadata.AggregateVersion;
        }
        if (position != owner.CommittedStreamPosition || version != owner.Version)
            throw new ImmutableContentUnavailableException("The source-use owner's exact hydrated history cut is not visible yet.");
        ct.ThrowIfCancellationRequested();
        source.Uses.Confirm(history, owner.Version, owner.CommittedStreamPosition);
        return owner;
    }
}
