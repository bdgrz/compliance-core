using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Aggregates;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Models;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NamedRecords;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Reads the authoritative current declaration of one provider from its own owner stream and the content store.
///     It replaces the tenant-wide register read: only the provider asked about is hydrated.
/// </summary>
public sealed class ProviderSource(IAggregateReader reader, IImmutableContentStore content)
{
    /// <summary>The current provider view, or null when the provider has no applied declaration.</summary>
    public async ValueTask<ProviderView?> GetAsync(Uuid tenantId, Uuid providerId, CancellationToken ct)
    {
        var owner = await reader.HydrateAsync(new Provider(tenantId, providerId), ct).ConfigureAwait(false);
        return owner.Current is { } current
            ? await ViewAsync(tenantId, providerId, current, ct).ConfigureAwait(false)
            : null;
    }

    /// <summary>The logical revision of the provider's owner; zero until its creation is applied.</summary>
    public async ValueTask<long> RevisionAsync(Uuid tenantId, Uuid providerId, CancellationToken ct) =>
        (await reader.HydrateAsync(new Provider(tenantId, providerId), ct).ConfigureAwait(false)).Revision;

    /// <summary>The view of one applied revision.</summary>
    public async ValueTask<ProviderView> ViewAsync(Uuid tenantId, Uuid providerId, NamedRecord.AppliedRecord applied,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(applied);
        var bytes = await content.ReadVerifiedAsync(tenantId.ToString(), applied.Content, ct).ConfigureAwait(false);
        var declared = JsonSerializer.Deserialize(bytes.Span, ComplianceCoreJsonContext.Default.ProviderContent)
            ?? throw new ImmutableContentIntegrityException("The provider content is empty.");
        return View(tenantId, providerId, applied.Revision, declared, applied.Actor, applied.ChangedAt);
    }

    /// <summary>The projected view of a provider revision.</summary>
    public static ProviderView View(Uuid tenantId, Uuid providerId, long revision, ProviderContent declared,
        Bdgrz.Compliance.Features.AccessControl.Models.ActorReference actor, DateTimeOffset recordedAt)
    {
        var normalized = ProviderRules.Normalize(declared);
        return new ProviderView(tenantId, providerId, revision, normalized, "manual", "active",
            ProviderRules.Unresolved(normalized), actor, recordedAt);
    }
}
