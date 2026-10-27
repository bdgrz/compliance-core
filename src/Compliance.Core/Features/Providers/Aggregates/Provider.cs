using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Aggregates;

/// <summary>
///     Owns one provider's content, version and change lifecycle. A governed provider name is unique within the
///     tenant, so every change first holds its name claim on <see cref="ProviderNameClaim" />; this owner then
///     serializes only its own record, never the tenant's other providers.
/// </summary>
public sealed class Provider : NamedRecord
{
    /// <summary>Stream area of the provider owners.</summary>
    public const string Area = "providers";

    /// <summary>The largest serialized provider declaration a change may freeze.</summary>
    public const int MaximumContentBytes = 48 * 1024;

    readonly Uuid _tenantId;

    /// <summary>Opens a provider owner; a non-empty <paramref name="selectedLogicalId" /> selects one logical operation.</summary>
    public Provider(Uuid tenantId, Uuid providerId, Uuid selectedLogicalId = default)
        : base("provider", providerId, new EventStreamAddress(tenantId.ToString(), Area, providerId.ToString()),
            selectedLogicalId)
    {
        _tenantId = tenantId;
        On<ProviderChangeAccepted>(ObserveAccepted);
        On<ProviderChangeCanceled>(ObserveCanceled);
        On<ProviderRecorded>(ObserveApplied);
        On<ProviderRevised>(ObserveApplied);
    }

    /// <inheritdoc />
    public override Result AcceptChange(OperationProvenance operation, bool create, long expectedRevision,
        ImmutableContentRef intent, ImmutableContentRef firstResponse)
    {
        var checkedChange = CheckAccept(operation, create, expectedRevision);
        if (!checkedChange.IsSuccess)
            return checkedChange;
        RaiseEvent(new ProviderChangeAccepted(Stream, create ? 0 : expectedRevision, Generation + 1, intent, null,
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
        if (Revision == 0)
            RaiseEvent(new ProviderRecorded(_tenantId, Id, operation.RootOperationId, content, actor, changedAt,
                operation, intent, null, result));
        else
            RaiseEvent(new ProviderRevised(_tenantId, Id, operation.RootOperationId, Revision + 1, content, actor,
                changedAt, operation, intent, null, result));
        return Result.Success;
    }

    /// <inheritdoc />
    public override Result CancelChange(OperationProvenance operation, ImmutableContentRef failureProof)
    {
        var checkedChange = CheckCancel(operation);
        if (!checkedChange.IsSuccess)
            return checkedChange;
        var pending = PendingForCancel;
        RaiseEvent(new ProviderChangeCanceled(Stream, pending.ExpectedRevision, pending.Generation, pending.Intent,
            failureProof, operation, null, null));
        return Result.Success;
    }
}
