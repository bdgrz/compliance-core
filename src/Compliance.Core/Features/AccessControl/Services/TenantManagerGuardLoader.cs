using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Loads exact pinned baseline and explicit historical episode/root selection; no candidate content writes occur here.</summary>
public sealed class TenantManagerGuardLoader(IAggregateReader reader, IImmutableContentStore content, TenantManagerGuardBootstrap bootstrap)
{
    public Task<LoadedGuard> LoadAsync(Uuid tenantId, Uuid selectedCommandId, CancellationToken ct) =>
        LoadAsync(tenantId, selectedCommandId, Uuid.Empty, Uuid.Empty, ct);

    public async Task<LoadedGuard> LoadAsync(Uuid tenantId, Uuid selectedCommandId, Uuid selectedEpisode, Uuid selectedRoot, CancellationToken ct)
    {
        var probe = await reader.HydrateAsync(new TenantManagerGuard(tenantId, selectedCommandId, selectedEpisode: selectedEpisode,
            selectedRoot: selectedRoot), ct).ConfigureAwait(false);
        var baseline = probe.IsEmpty
            ? await bootstrap.BuildAsync(tenantId, ct).ConfigureAwait(false)
            : await content.ReadJsonAsync<GuardBaseline>(tenantId.ToString(), probe.BaselineRef ??
                throw new ImmutableContentIntegrityException("The first guard fact must pin its exact source baseline."), ct).ConfigureAwait(false);
        var loaded = new LoadedGuard(tenantId, selectedCommandId, baseline, probe.BaselineRef, probe.SelectedEpisode, selectedRoot);
        loaded.Guard = await reader.HydrateAsync(loaded.Guard, ct).ConfigureAwait(false);
        return loaded;
    }
}
