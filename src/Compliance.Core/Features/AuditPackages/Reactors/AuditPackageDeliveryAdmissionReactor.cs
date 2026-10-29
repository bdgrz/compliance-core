using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.AuditPackages.Processes;
namespace Bdgrz.Compliance.Features.AuditPackages.Reactors;

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
