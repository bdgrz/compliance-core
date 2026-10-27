using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Durably starts the accepted package process before the source checkpoint can advance.</summary>
sealed partial class AuditPackageDeliveryAdmissionReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(AuditPackageManifest.Area), WorkloadName),
        IReactorHandler<AuditPackageDeliveryAdmitted>
{
    public const string WorkloadName = "AuditPackageDeliveryAdmissionV1";
    public async ValueTask HandleAsync(IReactorContext<AuditPackageDeliveryAdmitted> context, CancellationToken ct) =>
        _ = await process.StartAsync(AuditPackageDeliveryRecordingProcess.Name, context, context.Trigger.Intent,
            [new AuditPackageDelivery(context.Trigger.TenantId, context.Trigger.SubjectId, context.Trigger.PackageVersion,
                context.Trigger.Operation.LogicalId).Stream.ToString()],
            $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
}
