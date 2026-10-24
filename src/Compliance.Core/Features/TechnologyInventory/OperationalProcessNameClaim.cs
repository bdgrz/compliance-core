using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     The authoritative unique reservation of one normalized operational process name within a tenant. Its
///     identity is the typed UUIDv5 of the owner type, the tenant and the normalized name.
/// </summary>
public sealed class OperationalProcessNameClaim : NameClaim
{
    /// <summary>Owner type that seeds the identity.</summary>
    public const string OwnerType = "OperationalProcessNameClaim";

    /// <summary>Stream area of the operational process name claims.</summary>
    public const string Area = "operational-process-name-claims";

    /// <summary>Opens the claim at a known stream.</summary>
    public OperationalProcessNameClaim(Uuid tenantId, EventStreamAddress stream, Uuid selectedLogicalId = default)
        : base(Uuid.Parse(stream.Resource, System.Globalization.CultureInfo.InvariantCulture), stream,
            selectedLogicalId)
    {
        if (stream.Realm != tenantId.ToString() || stream.Area != Area)
            throw new ArgumentException("A operational process name claim lives in its tenant's claim area.", nameof(stream));
        On<OperationalProcessNameClaimed>(ObserveClaimed);
        On<OperationalProcessNameReleased>(ObserveReleased);
    }

    /// <summary>The claim stream of a normalized name.</summary>
    public static EventStreamAddress AddressOf(Uuid tenantId, string normalizedName) =>
        new(tenantId.ToString(), Area, AggregateIdentity.Create(OwnerType, tenantId, normalizedName).ToString());

    /// <inheritdoc />
    public override Result Claim(OperationProvenance operation, EventStreamAddress holder, long expectedRevision,
        long generation, ImmutableContentRef intent)
    {
        var checkedClaim = CheckClaim(operation, holder, generation);
        if (!checkedClaim.IsSuccess)
            return checkedClaim;
        RaiseEvent(new OperationalProcessNameClaimed(holder, expectedRevision, generation, intent, null, operation, null, null));
        return Result.Success;
    }

    /// <inheritdoc />
    public override Result Release(OperationProvenance operation, EventStreamAddress holder, long expectedRevision,
        long generation, ImmutableContentRef intent)
    {
        var checkedRelease = CheckRelease(operation, holder, generation);
        if (!checkedRelease.IsSuccess)
            return checkedRelease;
        RaiseEvent(new OperationalProcessNameReleased(holder, expectedRevision, generation, intent, null, operation, null, null));
        return Result.Success;
    }
}
