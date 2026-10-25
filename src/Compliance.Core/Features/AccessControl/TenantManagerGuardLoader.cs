using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

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
