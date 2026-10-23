using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     A tenant's guard hydrated with the contribution set its first fact pinned, and the means to create further
///     instances with that same set for an executor that rehydrates on a lost append race.
/// </summary>
public sealed record LoadedGuard(Uuid TenantId, Uuid SelectedCommandId, GuardBaseline Baseline,
    ImmutableContentRef BaselineRef)
{
    /// <summary>The hydrated guard.</summary>
    public TenantManagerGuard Guard { get; } = new(TenantId, SelectedCommandId, Baseline);

    /// <summary>A fresh, unhydrated guard seeded with the same contribution set.</summary>
    public TenantManagerGuard Create() => new(TenantId, SelectedCommandId, Baseline);
}
