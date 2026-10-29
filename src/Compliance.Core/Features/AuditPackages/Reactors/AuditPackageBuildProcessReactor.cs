using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.AuditPackages.Processes;
namespace Bdgrz.Compliance.Features.AuditPackages.Reactors;

/// <summary>Durably starts the accepted package process before the source checkpoint can advance.</summary>
sealed partial class AuditPackageBuildProcessReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(AuditPackageManifest.Area), WorkloadName),
        IReactorHandler<AuditPackageBuildRequested>
{
    public const string WorkloadName = "AuditPackageBuildProcessV1";
    public async ValueTask HandleAsync(IReactorContext<AuditPackageBuildRequested> context, CancellationToken ct) =>
        _ = await process.StartAsync(AuditPackageBuildProcess.Name, context, context.Trigger.Change, [context.Source.Stream.ToString()],
            $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
}
