using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.NamedRecords.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Processes;

/// <summary>
///     The authoritative unique reservation of one normalized operational process name within a tenant. Its
///     identity is the typed UUIDv5 of the owner type, the tenant and the normalized name.
/// </summary>
public sealed class OperationalProcessNameClaim : NameClaim
{
    readonly Uuid _tenantId;

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
            throw new ArgumentException("An operational process name claim lives in its tenant's claim area.", nameof(stream));
        _tenantId = tenantId;
        On<OperationalProcessNameClaimed>(ObserveOperationalProcessNameClaimed);
        On<OperationalProcessNameReleased>(ObserveOperationalProcessNameReleased);
    }

    /// <summary>The claim stream of a normalized name.</summary>
    public static EventStreamAddress AddressOf(Uuid tenantId, string normalizedName) =>
        new(tenantId.ToString(), Area, AggregateIdentity.Create(OwnerType, tenantId, normalizedName).ToString());

    /// <inheritdoc />
    public override Result Claim(OperationProvenance operation, EventStreamAddress holder, long expectedRevision,
        long generation, ImmutableContentRef intent)
    {
        if (!IsOperationalProcessOwner(holder))
            return InvalidHolder();
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
        if (!IsOperationalProcessOwner(holder))
            return InvalidHolder();
        var checkedRelease = CheckRelease(operation, holder, generation);
        if (!checkedRelease.IsSuccess)
            return checkedRelease;
        RaiseEvent(new OperationalProcessNameReleased(holder, expectedRevision, generation, intent, null, operation, null, null));
        return Result.Success;
    }

    void ObserveOperationalProcessNameClaimed(OperationalProcessNameClaimed fact)
    {
        if (!IsOperationalProcessOwner(fact.Owner))
            throw new InvalidOperationException("An operational process name claim must belong to a process in its tenant.");
        ObserveClaimed(fact);
    }

    void ObserveOperationalProcessNameReleased(OperationalProcessNameReleased fact)
    {
        if (!IsOperationalProcessOwner(fact.Owner))
            throw new InvalidOperationException("An operational process name release must belong to a process in its tenant.");
        ObserveReleased(fact);
    }

    bool IsOperationalProcessOwner(EventStreamAddress owner) =>
        owner is not null && owner.Realm == _tenantId.ToString() && owner.Area == InventoryRegisters.Area &&
        !string.IsNullOrWhiteSpace(owner.Resource) &&
        owner.Resource.StartsWith("operational-process-", StringComparison.Ordinal) &&
        Uuid.TryParse(owner.Resource["operational-process-".Length..],
            System.Globalization.CultureInfo.InvariantCulture, out var processId) && processId != Uuid.Empty;

    static Result InvalidHolder() => Result.Failure(new RequestError(RequestErrorKind.Validation,
        "An operational process name claim must belong to a process in its tenant."));
}
