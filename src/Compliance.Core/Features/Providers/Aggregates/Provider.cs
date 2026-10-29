using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NamedRecords.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Events;
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
        On<ProviderRecorded>(ObserveProviderApplied);
        On<ProviderRevised>(ObserveProviderApplied);
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

    void ObserveProviderApplied(INamedRecordAppliedFact fact)
    {
        if (fact.RecordTenantId != _tenantId)
            throw new InvalidOperationException("A provider revision must belong to its tenant.");
        ObserveApplied(fact);
    }
}
