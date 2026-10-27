using Bdgrz.Compliance.Features.NamedRecords.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
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
    readonly Uuid _tenantId;

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
        _tenantId = tenantId;
        On<LocationNameClaimed>(ObserveLocationNameClaimed);
        On<LocationNameReleased>(ObserveLocationNameReleased);
    }

    /// <summary>The claim stream of a normalized name.</summary>
    public static EventStreamAddress AddressOf(Uuid tenantId, string nameKind, string normalizedName) =>
        new(tenantId.ToString(), Area, AggregateIdentity.Create(OwnerType, tenantId, nameKind, normalizedName).ToString());

    /// <inheritdoc />
    public override Result Claim(OperationProvenance operation, EventStreamAddress holder, long expectedRevision,
        long generation, ImmutableContentRef intent)
    {
        if (!IsLocationOwner(holder))
            return InvalidHolder();
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
        if (!IsLocationOwner(holder))
            return InvalidHolder();
        var checkedRelease = CheckRelease(operation, holder, generation);
        if (!checkedRelease.IsSuccess)
            return checkedRelease;
        RaiseEvent(new LocationNameReleased(holder, expectedRevision, generation, intent, null, operation, null, null));
        return Result.Success;
    }

    void ObserveLocationNameClaimed(LocationNameClaimed fact)
    {
        if (!IsLocationOwner(fact.Owner))
            throw new InvalidOperationException("A location name claim must belong to a location in its tenant.");
        ObserveClaimed(fact);
    }

    void ObserveLocationNameReleased(LocationNameReleased fact)
    {
        if (!IsLocationOwner(fact.Owner))
            throw new InvalidOperationException("A location name release must belong to a location in its tenant.");
        ObserveReleased(fact);
    }

    bool IsLocationOwner(EventStreamAddress owner) =>
        owner is not null && owner.Realm == _tenantId.ToString() && owner.Area == InventoryRegisters.Area &&
        !string.IsNullOrWhiteSpace(owner.Resource) &&
        owner.Resource.StartsWith("location-", StringComparison.Ordinal) &&
        Uuid.TryParse(owner.Resource["location-".Length..], System.Globalization.CultureInfo.InvariantCulture,
            out var locationId) && locationId != Uuid.Empty;

    static Result InvalidHolder() => Result.Failure(new RequestError(RequestErrorKind.Validation,
        "A location name claim must belong to a location in its tenant."));
}
