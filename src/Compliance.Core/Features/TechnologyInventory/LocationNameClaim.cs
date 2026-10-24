using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     The authoritative unique reservation of one normalized location name within its location kind. Its
///     identity is the typed UUIDv5 of the owner type, the tenant, the kind and the normalized name.
/// </summary>
public sealed class LocationNameClaim : NameClaim
{
    /// <summary>Owner type that seeds the identity.</summary>
    public const string OwnerType = "LocationNameClaim";

    /// <summary>Stream area of the location name claims.</summary>
    public const string Area = "location-name-claims";

    /// <summary>Opens the claim at a known stream.</summary>
    public LocationNameClaim(Uuid tenantId, EventStreamAddress stream, Uuid selectedLogicalId = default)
        : base(Uuid.Parse(stream.Resource, System.Globalization.CultureInfo.InvariantCulture), stream,
            selectedLogicalId)
    {
        if (stream.Realm != tenantId.ToString() || stream.Area != Area)
            throw new ArgumentException("A location name claim lives in its tenant's claim area.", nameof(stream));
        On<LocationNameClaimed>(ObserveClaimed);
        On<LocationNameReleased>(ObserveReleased);
    }

    /// <summary>The claim stream of a normalized name.</summary>
    public static EventStreamAddress AddressOf(Uuid tenantId, string nameKind, string normalizedName) =>
        new(tenantId.ToString(), Area, AggregateIdentity.Create(OwnerType, tenantId, nameKind, normalizedName).ToString());

    /// <inheritdoc />
    public override Result Claim(OperationProvenance operation, EventStreamAddress holder, long expectedRevision,
        long generation, ImmutableContentRef intent)
    {
        var checkedClaim = CheckClaim(operation, holder, generation);
        if (!checkedClaim.IsSuccess)
            return checkedClaim;
        RaiseEvent(new LocationNameClaimed(holder, expectedRevision, generation, intent, null, operation, null, null));
        return Result.Success;
    }

    /// <inheritdoc />
    public override Result Release(OperationProvenance operation, EventStreamAddress holder, long expectedRevision,
        long generation, ImmutableContentRef intent)
    {
        var checkedRelease = CheckRelease(operation, holder, generation);
        if (!checkedRelease.IsSuccess)
            return checkedRelease;
        RaiseEvent(new LocationNameReleased(holder, expectedRevision, generation, intent, null, operation, null, null));
        return Result.Success;
    }
}
