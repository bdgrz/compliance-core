using System.Collections.Concurrent;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Indexes the complete immutable manifests of a launch and of a completion by item. A manifest is
///     digest-addressed and never changes, so its index is cached per content reference; the launch fan-out and the
///     completion process address every item of a campaign and must not read the whole manifest once per item.
/// </summary>
public sealed class AccessReviewManifests(IImmutableContentStore content)
{
    const int MaximumCached = 16;

    readonly ConcurrentDictionary<(string Realm, Uuid ContentId, string Sha256), object> _indexes = new();

    /// <summary>The frozen item views of a launch by item id.</summary>
    public async Task<IReadOnlyDictionary<Uuid, AccessReviewItemView>> ItemsAsync(string realm,
        ImmutableContentRef reference, CancellationToken ct = default)
    {
        var key = (realm, reference.ContentId, reference.Sha256);
        if (_indexes.TryGetValue(key, out var cached))
            return (IReadOnlyDictionary<Uuid, AccessReviewItemView>)cached;
        var items = await content.ReadJsonAsync<IReadOnlyList<AccessReviewItemView>>(realm, reference, ct)
            .ConfigureAwait(false);
        IReadOnlyDictionary<Uuid, AccessReviewItemView> index = items.ToDictionary(static item => item.ItemId);
        Remember(key, index);
        return index;
    }

    /// <summary>The frozen creation intent reference of every item of a launch by item id.</summary>
    public async Task<IReadOnlyDictionary<Uuid, ImmutableContentRef>> IntentsAsync(string realm,
        ImmutableContentRef reference, CancellationToken ct = default)
    {
        var key = (realm, reference.ContentId, reference.Sha256);
        if (_indexes.TryGetValue(key, out var cached))
            return (IReadOnlyDictionary<Uuid, ImmutableContentRef>)cached;
        var manifest = await content.ReadJsonAsync<AccessReviewItemIntentManifest>(realm, reference, ct)
            .ConfigureAwait(false);
        IReadOnlyDictionary<Uuid, ImmutableContentRef> index = manifest.Selections.ToDictionary(
            static selection => selection.ItemId, static selection => selection.Intent);
        Remember(key, index);
        return index;
    }

    /// <summary>The pinned source requirements of a completion by the item resource of each requirement.</summary>
    public async Task<IReadOnlyDictionary<string, SourceUseRequirement>> RequirementsAsync(string realm,
        ImmutableSelectionManifest manifest, CancellationToken ct = default)
    {
        var key = (realm, manifest.Content.ContentId, manifest.Content.Sha256);
        if (_indexes.TryGetValue(key, out var cached))
            return (IReadOnlyDictionary<string, SourceUseRequirement>)cached;
        var requirements = await content.ReadJsonAsync<IReadOnlyList<SourceUseRequirement>>(realm, manifest.Content,
            ct).ConfigureAwait(false);
        IReadOnlyDictionary<string, SourceUseRequirement> index = requirements.ToDictionary(
            static requirement => requirement.SourceOwner.Resource, StringComparer.Ordinal);
        Remember(key, index);
        return index;
    }

    /// <summary>
    ///     The canonical targets of a completion: every launched item that the completion pinned, ordered by the item
    ///     stream key. The caller compares the count with the pinned set to prove no item is omitted.
    /// </summary>
    public async Task<IReadOnlyList<string>> CompletionTargetsAsync(Uuid tenantId, Uuid campaignId,
        AccessReviewCampaignLaunched launched, ClosureIntent intent, CancellationToken ct = default)
    {
        var realm = AccessReviewContent.Realm(tenantId);
        var items = await ItemsAsync(realm, launched.ItemsManifest, ct).ConfigureAwait(false);
        var requirements = await RequirementsAsync(realm, intent.SourceUses, ct).ConfigureAwait(false);
        return [.. items.Keys.Select(itemId => AccessReviewCompletionTarget.Of(tenantId, campaignId, itemId))
            .Where(target => requirements.ContainsKey(target.Resource))
            .OrderBy(static target => target.Resource, Kernel.StreamKeyOrder.Instance)
            .Select(static target => target.ToString())];
    }

    void Remember((string Realm, Uuid ContentId, string Sha256) key, object index)
    {
        if (_indexes.Count >= MaximumCached)
            _indexes.Clear();
        _indexes[key] = index;
    }
}
