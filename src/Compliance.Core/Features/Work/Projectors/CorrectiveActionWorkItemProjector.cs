using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed class CorrectiveActionWorkItemProjector(
    ICorrectiveActionWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(),
        FitzCorrectiveActionWorkItemDirectory.ProjectorName)
{
    protected override ValueTask ProjectEventAsync(DomainEventRecord record, IProjectorContext context,
        CancellationToken ct) =>
        record.Event.Metadata.IsAudit || record.Stream.Area is not (FindingAggregate.Area or CorrectiveAction.Area)
            ? ValueTask.CompletedTask
            : projection.ApplyAsync(record.Event, ct);
}
