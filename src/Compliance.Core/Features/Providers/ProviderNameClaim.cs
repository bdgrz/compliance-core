using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     The authoritative unique reservation of one normalized governed provider name within a tenant. Its identity
///     is the typed UUIDv5 of the owner type, the tenant and the normalized name.
/// </summary>
public sealed class ProviderNameClaim : NameClaim
{
    /// <summary>Owner type that seeds the identity.</summary>
    public const string OwnerType = "ProviderNameClaim";

    /// <summary>Stream area of the provider name claims.</summary>
    public const string Area = "provider-name-claims";

    /// <summary>Opens the claim of a normalized name.</summary>
    public ProviderNameClaim(Uuid tenantId, string normalizedName, Uuid selectedLogicalId = default)
        : this(tenantId, AddressOf(tenantId, normalizedName), selectedLogicalId)
    {
    }

    /// <summary>Opens the claim at a known stream.</summary>
    public ProviderNameClaim(Uuid tenantId, EventStreamAddress stream, Uuid selectedLogicalId = default)
        : base(Uuid.Parse(stream.Resource, System.Globalization.CultureInfo.InvariantCulture), stream,
            selectedLogicalId)
    {
        if (stream.Realm != tenantId.ToString() || stream.Area != Area)
            throw new ArgumentException("A provider name claim lives in its tenant's claim area.", nameof(stream));
        On<ProviderNameClaimed>(ObserveClaimed);
        On<ProviderNameReleased>(ObserveReleased);
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
        RaiseEvent(new ProviderNameClaimed(holder, expectedRevision, generation, intent, null, operation, null, null));
        return Result.Success;
    }

    /// <inheritdoc />
    public override Result Release(OperationProvenance operation, EventStreamAddress holder, long expectedRevision,
        long generation, ImmutableContentRef intent)
    {
        var checkedRelease = CheckRelease(operation, holder, generation);
        if (!checkedRelease.IsSuccess)
            return checkedRelease;
        RaiseEvent(new ProviderNameReleased(holder, expectedRevision, generation, intent, null, operation, null, null));
        return Result.Success;
    }
}
