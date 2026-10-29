using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Reactors;

/// <summary>
///     Reconciles the assignees of every newly recorded acceptance or staff assignment against the directory status
///     changes that already happened, so an assignment accepted after a status change is denied like an earlier one.
/// </summary>
sealed partial class AcceptedStaffDirectoryReconciliationReactor(IProjectionCheckpointStore checkpoints,
    DirectoryReevaluationDiscovery discovery)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(ServiceEngagementIndependence.Area), WorkloadName),
        IReactorHandler<ServiceEngagementAcceptanceRecorded>,
        IReactorHandler<ServiceEngagementStaffAssigned>
{
    public const string WorkloadName = "AcceptedStaffDirectoryReconciliationV1";

    public ValueTask HandleAsync(IReactorContext<ServiceEngagementAcceptanceRecorded> context, CancellationToken ct)
    {
        Verify(context, context.Trigger.TenantId, context.Trigger.Operation.Owner);
        return discovery.ReconcileAcceptanceAsync(context, context.Trigger, ct);
    }

    public ValueTask HandleAsync(IReactorContext<ServiceEngagementStaffAssigned> context, CancellationToken ct)
    {
        Verify(context, context.Trigger.TenantId, context.Trigger.Operation.Owner);
        return discovery.ReconcileAcceptanceAsync(context, context.Trigger, ct);
    }

    void Verify(IReactorContext context, Uuid tenantId, EventStreamAddress owner)
    {
        if (Pattern.IsTenantTemplate || Pattern.Realm != tenantId.ToString() || context.Source.Stream != owner ||
            owner.Area != ServiceEngagementIndependence.Area)
            throw new InvalidOperationException(
                "Accepted staff reconciliation requires its bound authoritative engagement stream.");
    }
}
