using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.AccessControl.Readers;

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
