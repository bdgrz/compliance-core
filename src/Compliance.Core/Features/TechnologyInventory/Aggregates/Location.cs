using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NamedRecords.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Aggregates;

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
        On<LocationRevisionRecorded>(ObserveLocationRevision);
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

    void ObserveLocationRevision(LocationRevisionRecorded fact)
    {
        if (fact.TenantId != _tenantId)
            throw new InvalidOperationException("A location revision must belong to its tenant.");
        ObserveApplied(fact);
    }
}
