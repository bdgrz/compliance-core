using System.Text.Json;
using Bdgrz.Compliance.Features.NamedRecords;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>The location family of the shared name-change machinery: its descriptor and receiver names.</summary>
public static class LocationNames
{
    /// <summary>Catalog name of the receiver that acquires a location name claim.</summary>
    public const string ClaimReceiver = "ClaimLocationName";

    /// <summary>Catalog name of the receiver that applies the accepted location change.</summary>
    public const string ApplyReceiver = "ApplyLocationChange";

    /// <summary>Catalog name of the receiver that releases a location name claim.</summary>
    public const string ReleaseReceiver = "ReleaseLocationName";

    /// <summary>Catalog name of the abort receiver that cancels a pending location change.</summary>
    public const string CancelReceiver = "CancelLocationChange";

    /// <summary>Catalog name of the location name-change process.</summary>
    public const string ProcessName = "LocationNameChangeProcess";

    /// <summary>The descriptor the shared admission, receivers and runner use for locations.</summary>
    public static NamedRecordKindDescriptor Descriptor { get; } = new(NamedRecordKind.Location, "location",
        LocationNameClaim.OwnerType, LocationNameClaim.Area, "RecordLocation", "ReviseLocation", ClaimReceiver,
        ApplyReceiver, ReleaseReceiver, CancelReceiver, "location-content.v1", LocationRevisionRecorded.ContentSchema,
        LocationRevisionRecorded.ContentSchema, ProcessName,
        (tenantId, locationId, logicalId) => new Location(tenantId, locationId, logicalId),
        (tenantId, stream, logicalId) => new LocationNameClaim(tenantId, stream, logicalId),
        ReadName, ResultOf);

    /// <summary>The name a serialized location content claims: only an active location claims, within its kind.</summary>
    public static NamedContentName ReadName(ReadOnlyMemory<byte> content)
    {
        var location = JsonSerializer.Deserialize(content.Span, ComplianceCoreJsonContext.Default.LocationContent)
            ?? throw new InvalidOperationException("The location content is empty.");
        return new NamedContentName(location.Kind,
            location.Lifecycle == TechnologyInventoryRules.Active ? NameNormalization.Normalize(location.Name) : null,
            location.Lifecycle);
    }

    static (string Schema, byte[] Bytes) ResultOf(bool create, Uuid locationId, long revision) => create
        ? ("location-registration.v1", JsonSerializer.SerializeToUtf8Bytes(new LocationRegistration(locationId),
            ComplianceCoreJsonContext.Default.LocationRegistration))
        : (Bdgrz.Compliance.Features.NativeOperations.UnitResult.Schema, JsonSerializer.SerializeToUtf8Bytes(
            new Bdgrz.Compliance.Features.NativeOperations.UnitResult(), ComplianceCoreJsonContext.Default.UnitResult));
}
