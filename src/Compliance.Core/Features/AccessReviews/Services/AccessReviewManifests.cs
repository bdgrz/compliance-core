using System.Collections.Concurrent;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.AccessReviews.Services;

/// <summary>
///     Indexes the complete immutable manifests of a launch and of a completion by item. A manifest is
///     digest-addressed and never changes, so its index is cached per content reference; the launch fan-out and the
///     completion process address every item of a campaign and must not read the whole manifest once per item.
/// </summary>
public sealed class AccessReviewManifests(IImmutableContentStore content)
{
    const int MaximumCached = 16;

    readonly ConcurrentDictionary<(string Realm, string Kind, ImmutableContentRef Content, ImmutableSelectionManifest? Selection), object> _indexes = new();

    /// <summary>The frozen item views of a launch by item id.</summary>
    public async Task<IReadOnlyDictionary<Uuid, AccessReviewItemView>> ItemsAsync(string realm,
        ImmutableContentRef reference, CancellationToken ct = default)
    {
        var key = (realm, nameof(ItemsAsync), reference, (ImmutableSelectionManifest?)null);
        if (_indexes.TryGetValue(key, out var cached))
            return (IReadOnlyDictionary<Uuid, AccessReviewItemView>)cached;
        var items = await content.ReadJsonAsync<IReadOnlyList<AccessReviewItemView>>(realm, reference, ct)
            .ConfigureAwait(false);
        var index = Index(items, static item => item.ItemId);
        Remember(key, index);
        return index;
    }

    /// <summary>The frozen creation intent reference of every item of a launch by item id.</summary>
    public async Task<IReadOnlyDictionary<Uuid, ImmutableContentRef>> IntentsAsync(string realm,
        ImmutableContentRef reference, CancellationToken ct = default)
    {
        var key = (realm, nameof(IntentsAsync), reference, (ImmutableSelectionManifest?)null);
        if (_indexes.TryGetValue(key, out var cached))
            return (IReadOnlyDictionary<Uuid, ImmutableContentRef>)cached;
        var manifest = await content.ReadJsonAsync<AccessReviewItemIntentManifest>(realm, reference, ct)
            .ConfigureAwait(false);
        IReadOnlyDictionary<Uuid, ImmutableContentRef> index = Index(manifest.Selections, static selection => selection.ItemId)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value.Intent);
        Remember(key, index);
        return index;
    }

    /// <summary>The pinned source requirements of a completion by the item resource of each requirement.</summary>
    public async Task<IReadOnlyDictionary<string, SourceUseRequirement>> RequirementsAsync(string realm,
        ImmutableSelectionManifest manifest, CancellationToken ct = default)
    {
        var key = (realm, nameof(RequirementsAsync), manifest.Content, (ImmutableSelectionManifest?)manifest);
        if (_indexes.TryGetValue(key, out var cached))
            return (IReadOnlyDictionary<string, SourceUseRequirement>)cached;
        Check(manifest, AccessReviewContent.SourceUseItemSchema);
        var requirements = await ImmutableSelectionManifests.ReadAsync<SourceUseRequirement>(content, realm, manifest,
            ct).ConfigureAwait(false);
        RequireOrder(requirements.Select(static value => value.SourceOwner.Resource));
        var index = Index(requirements, static requirement => requirement.SourceOwner.Resource);
        Remember(key, index);
        return index;
    }

    /// <summary>The frozen item intents of a bulk decision by the item stream resource of each selected item.</summary>
    public async Task<IReadOnlyDictionary<string, BulkAccessDecisionItemIntent>> BulkItemsAsync(string realm,
        ImmutableSelectionManifest manifest, CancellationToken ct = default)
    {
        var key = (realm, nameof(BulkItemsAsync), manifest.Content, (ImmutableSelectionManifest?)manifest);
        if (_indexes.TryGetValue(key, out var cached))
            return (IReadOnlyDictionary<string, BulkAccessDecisionItemIntent>)cached;
        Check(manifest, BulkAccessDecisionItemIntent.Schema);
        var items = await ImmutableSelectionManifests.ReadAsync<BulkAccessDecisionItemIntent>(content, realm, manifest,
            ct).ConfigureAwait(false);
        RequireOrder(items.Select(static value => value.ItemOwner.Resource));
        var index = Index(items, static item => item.ItemOwner.Resource);
        Remember(key, index);
        return index;
    }

    /// <summary>The admitted bulk decision batches a completion drains, by the id of each selected item.</summary>
    public async Task<IReadOnlyDictionary<Uuid, IReadOnlyList<AccessReviewAdmittedBatchSelection>>> DrainsAsync(
        string realm, ImmutableSelectionManifest manifest, CancellationToken ct = default)
    {
        var key = (realm, nameof(DrainsAsync), manifest.Content, (ImmutableSelectionManifest?)manifest);
        if (_indexes.TryGetValue(key, out var cached))
            return (IReadOnlyDictionary<Uuid, IReadOnlyList<AccessReviewAdmittedBatchSelection>>)cached;
        Check(manifest, AccessReviewContent.AdmittedChildrenSchema);
        var selections = await ImmutableSelectionManifests.ReadAsync<AccessReviewAdmittedBatchSelection>(content,
            realm, manifest, ct).ConfigureAwait(false);
        if (selections.Select(static selection => (selection.ItemId, selection.DecisionBatchId)).Distinct().Count() != selections.Count)
            throw new ImmutableContentIntegrityException("The drained selection repeats an item/batch admission.");
        IReadOnlyDictionary<Uuid, IReadOnlyList<AccessReviewAdmittedBatchSelection>> index = selections
            .GroupBy(static selection => selection.ItemId)
            .ToDictionary(static group => group.Key, static group => (IReadOnlyList<AccessReviewAdmittedBatchSelection>)[.. group]);
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
            .OrderBy(static target => target.Resource, Bdgrz.Compliance.Features.Kernel.Services.StreamKeyOrder.Instance)
            .Select(static target => target.ToString())];
    }

    static void Check(ImmutableSelectionManifest manifest, string schema)
    {
        if (manifest.ItemSchema != schema || manifest.Order != SelectionOrder.CanonicalKey)
            throw new ImmutableContentIntegrityException("The selection has another schema or ordering contract.");
    }

    static void RequireOrder(IEnumerable<string> keys)
    {
        string? previous = null;
        foreach (var key in keys)
        {
            if (previous is not null && Bdgrz.Compliance.Features.Kernel.Services.StreamKeyOrder.Instance.Compare(previous, key) >= 0)
                throw new ImmutableContentIntegrityException("The complete selection is not in unique canonical owner order.");
            previous = key;
        }
    }

    static Dictionary<TKey, TValue> Index<TKey, TValue>(IEnumerable<TValue> values, Func<TValue, TKey> keyOf)
        where TKey : notnull
    {
        var result = new Dictionary<TKey, TValue>();
        foreach (var value in values)
            if (!result.TryAdd(keyOf(value), value))
                throw new ImmutableContentIntegrityException("The complete immutable manifest repeats a selected identity.");
        return result;
    }

    void Remember((string Realm, string Kind, ImmutableContentRef Content, ImmutableSelectionManifest? Selection) key, object index)
    {
        if (_indexes.Count >= MaximumCached)
            _indexes.Clear();
        _indexes[key] = index;
    }
}
