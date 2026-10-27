using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Seeds the exact immutable baseline, then certifies full guard history before any current/read/replay authority escapes.</summary>
public sealed class TenantManagerGuardAggregateReader(IAggregateReader inner, IDomainEventReader events,
    IImmutableContentStore content, TimeProvider clock) : IAggregateReader
{
    public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default) where TAggregate : Aggregate
    {
        if (aggregate is not TenantManagerGuard requested)
            return await inner.HydrateAsync(aggregate, ct).ConfigureAwait(false);
        var input = requested.TakeSeed();
        var probe = await inner.HydrateAsync(requested, ct).ConfigureAwait(false);
        var baseline = probe.IsEmpty
            ? input ?? await new TenantManagerGuardBootstrap(events, inner, clock).BuildAsync(probe.TenantId, ct).ConfigureAwait(false)
            : await content.ReadJsonAsync<GuardBaseline>(probe.Stream.Realm, probe.BaselineRef ??
                throw new ImmutableContentIntegrityException("The first guard fact must pin its original source baseline."), ct).ConfigureAwait(false);
        if (!probe.IsEmpty || input is null)
        {
            // An append may have won after the caller captured an empty-stream baseline. Only the winner's pinned cut is authoritative.
            probe = await inner.HydrateAsync(new TenantManagerGuard(probe.TenantId, probe.Acceptance.SelectedLogicalId,
                baseline, probe.SelectedEpisode, probe.SelectedRoot), ct).ConfigureAwait(false);
            _ = probe.TakeSeed();
        }
        _ = await TenantManagerGuardHistory.ReadAsync(events, probe, baseline, ct).ConfigureAwait(false);
        return (TAggregate)(Aggregate)probe;
    }
}
