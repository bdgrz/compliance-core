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
        CassieFindingClosureWorkItemDirectory.ProjectorName)
{
    protected override async ValueTask<Result> ProjectEventAsync(DomainEventRecord record, IProjectorContext context,
        CancellationToken ct)
    {
        if (record.Event.Metadata.IsAudit || record.Stream.Area is not (FindingAggregate.Area or CorrectiveAction.Area))
            return Result.Success;
        await projection.ApplyAsync(record.Event, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
