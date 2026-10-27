using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;

/// <summary>
///     Owns one operational process's content, version and change lifecycle. Active names are unique, so every change that needs a
///     name first holds its claim on <see cref="OperationalProcessNameClaim" />; this owner then serializes only its own
///     record, never the tenant's other operational processs.
/// </summary>
public sealed class OperationalProcess : NamedRecord
{
    readonly Uuid _tenantId;

    /// <summary>Opens a operational process owner; a non-empty <paramref name="selectedLogicalId" /> selects one logical operation.</summary>
    public OperationalProcess(Uuid tenantId, Uuid operationalProcessId, Uuid selectedLogicalId = default)
        : base("operational process", operationalProcessId, InventoryRegisters.OperationalProcessAddress(tenantId, operationalProcessId),
            selectedLogicalId)
    {
        _tenantId = tenantId;
        On<OperationalProcessChangeAccepted>(ObserveAccepted);
        On<OperationalProcessChangeCanceled>(ObserveCanceled);
        On<OperationalProcessRevisionRecorded>(ObserveOperationalProcessRevision);
    }

    /// <inheritdoc />
    public override Result AcceptChange(OperationProvenance operation, bool create, long expectedRevision,
        ImmutableContentRef intent, ImmutableContentRef firstResponse)
    {
        var checkedChange = CheckAccept(operation, create, expectedRevision);
        if (!checkedChange.IsSuccess)
            return checkedChange;
        RaiseEvent(new OperationalProcessChangeAccepted(Stream, create ? 0 : expectedRevision, Generation + 1, intent, null,
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
        RaiseEvent(new OperationalProcessRevisionRecorded(_tenantId, Id, Revision + 1, content, actor, changedAt, operation, intent, null,
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
        RaiseEvent(new OperationalProcessChangeCanceled(Stream, pending.ExpectedRevision, pending.Generation, pending.Intent,
            failureProof, operation, null, null));
        return Result.Success;
    }

    void ObserveOperationalProcessRevision(OperationalProcessRevisionRecorded fact)
    {
        if (fact.TenantId != _tenantId)
            throw new InvalidOperationException("An operational process revision must belong to its tenant.");
        ObserveApplied(fact);
    }
}
