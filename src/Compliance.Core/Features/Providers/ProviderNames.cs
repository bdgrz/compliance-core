using System.Text.Json;
using Bdgrz.Compliance.Features.NamedRecords;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>The provider family of the shared name-change machinery: its descriptor and receiver names.</summary>
public static class ProviderNames
{
    /// <summary>The name kind of provider claims; provider names are unique tenant-wide.</summary>
    public const string NameKind = "provider";

    /// <summary>Catalog name of the receiver that acquires a provider name claim.</summary>
    public const string ClaimReceiver = "ClaimProviderName";

    /// <summary>Catalog name of the receiver that applies the accepted provider change.</summary>
    public const string ApplyReceiver = "ApplyProviderChange";

    /// <summary>Catalog name of the receiver that releases a provider name claim.</summary>
    public const string ReleaseReceiver = "ReleaseProviderName";

    /// <summary>Catalog name of the abort receiver that cancels a pending provider change.</summary>
    public const string CancelReceiver = "CancelProviderChange";

    /// <summary>Catalog name of the provider name-change process.</summary>
    public const string ProcessName = "ProviderNameChangeProcess";

    /// <summary>The descriptor the shared admission, receivers and runner use for providers.</summary>
    public static NamedRecordKindDescriptor Descriptor { get; } = new(NamedRecordKind.Provider, "provider",
        ProviderNameClaim.OwnerType, ProviderNameClaim.Area, "RecordProvider", "ReviseProvider", ClaimReceiver,
        ApplyReceiver, ReleaseReceiver, CancelReceiver, "provider-content.v1", ProviderRecorded.ContentSchema,
        ProviderRevised.ContentSchema, ProcessName,
        (tenantId, providerId, logicalId) => new Provider(tenantId, providerId, logicalId),
        (tenantId, stream, logicalId) => new ProviderNameClaim(tenantId, stream, logicalId),
        ReadName, ResultOf);

    /// <summary>The name a serialized provider content claims: always active, never partitioned by kind.</summary>
    public static NamedContentName ReadName(ReadOnlyMemory<byte> content)
    {
        var provider = JsonSerializer.Deserialize(content.Span, ComplianceCoreJsonContext.Default.ProviderContent)
            ?? throw new InvalidOperationException("The provider content is empty.");
        return new NamedContentName(NameKind, NameNormalization.Normalize(provider.Name), null);
    }

    static (string Schema, byte[] Bytes) ResultOf(bool create, Uuid providerId, long revision) =>
        ("provider-registration.v1", JsonSerializer.SerializeToUtf8Bytes(new ProviderRegistration(providerId, revision),
            ComplianceCoreJsonContext.Default.ProviderRegistration));
}
