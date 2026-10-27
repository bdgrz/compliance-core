using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Work.Projections;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Projectors;

public sealed class FindingClosureWorkItemProjector(
    IFindingClosureWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(),
        FitzFindingClosureWorkItemDirectory.ProjectorName)
{
    protected override ValueTask ProjectEventAsync(DomainEventRecord record, IProjectorContext context,
        CancellationToken ct) =>
        record.Event.Metadata.IsAudit || record.Stream.Area is not (FindingAggregate.Area or CorrectiveAction.Area)
            ? ValueTask.CompletedTask
            : projection.ApplyAsync(record.Event, ct);
}
