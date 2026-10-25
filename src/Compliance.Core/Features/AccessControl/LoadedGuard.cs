using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>One exact baseline and episode/root selection for executor rehydration; no history is retained in command authority.</summary>
public sealed record LoadedGuard(Uuid TenantId, Uuid SelectedCommandId, GuardBaseline Baseline,
    ImmutableContentRef? BaselineRef, Uuid SelectedEpisode = default, Uuid SelectedRoot = default)
{
    public TenantManagerGuard Guard { get; internal set; } = new(TenantId, SelectedCommandId, Baseline, SelectedEpisode, SelectedRoot);
    public TenantManagerGuard Create() => new(TenantId, SelectedCommandId, Baseline, SelectedEpisode, SelectedRoot);
}
