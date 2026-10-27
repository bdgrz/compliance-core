using Bdgrz.Compliance.Features.Proofs.Models;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Content;

/// <summary>JSON helpers over <see cref="IImmutableContentStore"/> using the generated serializer context.</summary>
public static class ImmutableContentStoreExtensions
{
    /// <summary>The media type recorded for JSON content.</summary>
    public const string JsonMediaType = "application/json";

    /// <summary>Serializes the value through the generated context and publishes it as application/json.</summary>
    public static Task<ImmutableContentRef> WriteJsonAsync<T>(this IImmutableContentStore store, string realm,
        Uuid contentId, string schema, T value, CancellationToken ct = default) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(value);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Info<T>());
        return store.WriteVerifiedAsync(realm, contentId, schema, JsonMediaType, bytes, ct);
    }

    /// <summary>Reads verified application/json content and deserializes it through the generated context.</summary>
    public static async Task<T> ReadJsonAsync<T>(this IImmutableContentStore store, string realm,
        ImmutableContentRef reference, CancellationToken ct = default) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(reference);
        if (reference.MediaType != JsonMediaType)
            throw new ImmutableContentIntegrityException("The content reference is not application/json.");
        var bytes = await store.ReadVerifiedAsync(realm, reference, ct).ConfigureAwait(false);
        try
        {
            return JsonSerializer.Deserialize(bytes.Span, Info<T>())
                ?? throw new ImmutableContentIntegrityException("The stored content is JSON null.");
        }
        catch (JsonException exception)
        {
            throw new ImmutableContentIntegrityException($"The stored content is not valid {typeof(T).Name} JSON: {exception.Message}");
        }
    }

    static JsonTypeInfo<T> Info<T>() =>
        ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
        ?? throw new InvalidOperationException($"No serialization metadata is registered for {typeof(T).FullName}.");
}
