using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Work.Projections;
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
