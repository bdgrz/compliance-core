using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     Owns one location's content, version and change lifecycle. Active names are unique, so every change that needs a
///     name first holds its claim on <see cref="LocationNameClaim" />; this owner then serializes only its own
///     record, never the tenant's other locations.
/// </summary>
public sealed class Location : NamedRecord
{
    readonly Uuid _tenantId;

    /// <summary>Opens a location owner; a non-empty <paramref name="selectedLogicalId" /> selects one logical operation.</summary>
    public Location(Uuid tenantId, Uuid locationId, Uuid selectedLogicalId = default)
        : base("location", locationId, InventoryRegisters.LocationAddress(tenantId, locationId),
            selectedLogicalId)
    {
        _tenantId = tenantId;
        On<LocationChangeAccepted>(ObserveAccepted);
        On<LocationChangeCanceled>(ObserveCanceled);
        On<LocationRevisionRecorded>(ObserveApplied);
    }

    /// <inheritdoc />
    public override Result AcceptChange(OperationProvenance operation, bool create, long expectedRevision,
        ImmutableContentRef intent, ImmutableContentRef firstResponse)
    {
        var checkedChange = CheckAccept(operation, create, expectedRevision);
        if (!checkedChange.IsSuccess)
            return checkedChange;
        RaiseEvent(new LocationChangeAccepted(Stream, create ? 0 : expectedRevision, Generation + 1, intent, null,
            operation, firstResponse, null));
        return Result.Success;
    }

    /// <inheritdoc />
    public override Result ApplyChange(OperationProvenance operation, ImmutableContentRef intent,
        ImmutableContentRef content, ActorReference actor, DateTimeOffset changedAt, ImmutableContentRef result)
    {
        var checkedChange = CheckApply(operation, intent);
        if (!checkedChange.IsSuccess)
            return checkedChange;
        RaiseEvent(new LocationRevisionRecorded(_tenantId, Id, Revision + 1, content, actor, changedAt, operation, intent, null,
            result));
        return Result.Success;
    }

    /// <inheritdoc />
    public override Result CancelChange(OperationProvenance operation, ImmutableContentRef failureProof)
    {
        var checkedChange = CheckCancel(operation);
        if (!checkedChange.IsSuccess)
            return checkedChange;
        var pending = PendingForCancel;
        RaiseEvent(new LocationChangeCanceled(Stream, pending.ExpectedRevision, pending.Generation, pending.Intent,
            failureProof, operation, null, null));
        return Result.Success;
    }
}
