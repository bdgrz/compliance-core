using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Content;

/// <summary>
/// Writes and reads complete immutable selections. The manifest names the stored JSON array, its item count and the
/// digest of its exact bytes, so a reader verifies that nothing was omitted, added or reordered. Items must be
/// registered in <see cref="ComplianceCoreJsonContext"/> as arrays.
/// </summary>
public static class ImmutableSelectionManifests
{
    /// <summary>Stores the items as one immutable JSON array and returns the manifest that covers all of them.</summary>
    public static async Task<ImmutableSelectionManifest> WriteSelectionAsync<T>(this IImmutableContentStore store,
        string realm, Uuid contentId, string itemSchema, IReadOnlyList<T> items, SelectionOrder order,
        CancellationToken ct = default) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemSchema);
        ArgumentNullException.ThrowIfNull(items);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(items.ToArray(), Info<T>());
        var reference = await store.WriteVerifiedAsync(realm, contentId, itemSchema,
            ImmutableContentStoreExtensions.JsonMediaType, bytes, ct).ConfigureAwait(false);
        return new ImmutableSelectionManifest(reference, itemSchema, items.Count, order, ContentDigest.Sha256(bytes));
    }

    /// <summary>Reads every item of the selection; any count, digest or schema mismatch fails closed.</summary>
    /// <exception cref="ImmutableContentIntegrityException">The stored selection does not match its manifest.</exception>
    public static async Task<IReadOnlyList<T>> ReadSelectionAsync<T>(this IImmutableContentStore store, string realm,
        ImmutableSelectionManifest manifest, CancellationToken ct = default) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(manifest);
        if (!string.Equals(manifest.Content.Schema, manifest.ItemSchema, StringComparison.Ordinal))
            throw new ImmutableContentIntegrityException("The selection content schema differs from its item schema.");
        var bytes = await store.ReadVerifiedAsync(realm, manifest.Content, ct).ConfigureAwait(false);
        if (!string.Equals(ContentDigest.Sha256(bytes.Span), manifest.ItemsSha256, StringComparison.Ordinal))
            throw new ImmutableContentIntegrityException("The selection items do not match the manifest digest.");
        IReadOnlyList<T>? items;
        try
        {
            items = JsonSerializer.Deserialize(bytes.Span, Info<T>()) as IReadOnlyList<T>;
        }
        catch (JsonException exception)
        {
            throw new ImmutableContentIntegrityException(
                $"The stored selection is not a valid {typeof(T).Name} array: {exception.Message}");
        }

        if (items is null || items.Count != manifest.Count)
            throw new ImmutableContentIntegrityException("The selection item count differs from its manifest.");
        return Array.AsReadOnly(items.ToArray());
    }

    /// <summary>Compatibility entry point for callers that write complete selections.</summary>
    public static Task<ImmutableSelectionManifest> WriteAsync<T>(IImmutableContentStore store, string realm,
        Uuid contentId, string itemSchema, IReadOnlyList<T> items, SelectionOrder order,
        CancellationToken ct = default) where T : notnull =>
        store.WriteSelectionAsync(realm, contentId, itemSchema, items, order, ct);

    /// <summary>Compatibility entry point for callers that read complete selections.</summary>
    public static Task<IReadOnlyList<T>> ReadAsync<T>(IImmutableContentStore store, string realm,
        ImmutableSelectionManifest manifest, CancellationToken ct = default) where T : notnull =>
        store.ReadSelectionAsync<T>(realm, manifest, ct);

    static JsonTypeInfo Info<T>() =>
        ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T[]))
        ?? ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(IReadOnlyList<T>))
        ?? throw new InvalidOperationException($"No serialization metadata is registered for a selection of {typeof(T).FullName}.");
}
