using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Hydrates the tenant manager guard together with its compact contribution set. A tenant whose guard stream is
///     empty gets the set from an authoritative source cut, frozen as content that the first fact will pin; any other
///     guard gets exactly the set its first fact pinned, so every reader sees the same baseline.
/// </summary>
public sealed class TenantManagerGuardLoader(IAggregateReader reader, IImmutableContentStore content,
    TenantManagerGuardBootstrap bootstrap)
{
    /// <summary>Loads the guard of the tenant, selected for the logical operation <paramref name="selectedCommandId" />.</summary>
    public async Task<LoadedGuard> LoadAsync(Uuid tenantId, Uuid selectedCommandId, CancellationToken ct)
    {
        var realm = tenantId.ToString();
        var probe = await reader.HydrateAsync(new TenantManagerGuard(tenantId, selectedCommandId), ct)
            .ConfigureAwait(false);
        GuardBaseline baseline;
        ImmutableContentRef baselineRef;
        if (probe.IsEmpty)
        {
            baseline = await bootstrap.BuildAsync(tenantId, ct).ConfigureAwait(false);
            baselineRef = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), GuardBaseline.Schema, baseline, ct)
                .ConfigureAwait(false);
        }
        else
        {
            baselineRef = probe.BaselineRef ?? throw new InvalidOperationException(
                "The first guard fact must reference the source cut that seeded its contributions.");
            baseline = await content.ReadJsonAsync<GuardBaseline>(realm, baselineRef, ct).ConfigureAwait(false);
        }

        var loaded = new LoadedGuard(tenantId, selectedCommandId, baseline, baselineRef);
        await reader.HydrateAsync(loaded.Guard, ct).ConfigureAwait(false);
        return loaded;
    }
}
