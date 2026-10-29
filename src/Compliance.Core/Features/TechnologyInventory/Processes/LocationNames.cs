using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NamedRecords.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Commands;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Processes;

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
        : (Bdgrz.Compliance.Features.NativeOperations.Models.UnitResult.Schema, JsonSerializer.SerializeToUtf8Bytes(
            new Bdgrz.Compliance.Features.NativeOperations.Models.UnitResult(), ComplianceCoreJsonContext.Default.UnitResult));
}
