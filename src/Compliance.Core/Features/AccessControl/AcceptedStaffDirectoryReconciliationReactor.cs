using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

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
