using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Bdgrz.Compliance.Features.Kernel;

/// <summary>
/// Digest of the authored fields of an intent: the intent is serialized through the source-generated serializer
/// context in declaration order with wire property names, timestamps normalized to UTC and decimals to G29, and the
/// UTF-8 document is hashed. C# type and member names never enter the digest, so ordinary renames keep exact retries
/// comparable; the wire contract is what changes it.
/// </summary>
public static class AuthoredIntentDigest
{
    static readonly ConcurrentDictionary<IJsonTypeInfoResolver, JsonSerializerOptions> Options = new();

    /// <summary>Computes the lowercase SHA-256 hex digest of the canonical encoding of an intent registered in <see cref="ComplianceCoreJsonContext"/>.</summary>
    public static string Compute<T>(T intent) where T : notnull => Compute(intent, ComplianceCoreJsonContext.Default);

    /// <summary>Computes the digest using the serialization metadata of <paramref name="resolver"/>.</summary>
    public static string Compute<T>(T intent, IJsonTypeInfoResolver resolver) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(resolver);
        var options = Options.GetOrAdd(resolver, CreateOptions);
        var info = options.GetTypeInfo(typeof(T));
        return ContentDigest.Sha256(JsonSerializer.SerializeToUtf8Bytes(intent, info));
    }

    static JsonSerializerOptions CreateOptions(IJsonTypeInfoResolver resolver)
    {
        var options = new JsonSerializerOptions(ComplianceCoreJsonContext.Default.Options)
        {
            TypeInfoResolver = resolver,
            WriteIndented = false
        };
        options.Converters.Add(new UtcDateTimeOffsetJsonConverter());
        options.Converters.Add(new G29DecimalJsonConverter());
        options.MakeReadOnly(populateMissingResolver: false);
        return options;
    }
}
