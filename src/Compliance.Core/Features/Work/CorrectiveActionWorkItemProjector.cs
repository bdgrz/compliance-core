using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed class CorrectiveActionWorkItemProjector(
    ICorrectiveActionWorkItemProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(),
        FitzCorrectiveActionWorkItemDirectory.ProjectorName)
{
    protected override ValueTask ProjectEventAsync(DomainEventRecord record, IProjectorContext context,
        CancellationToken ct)
    {
        if (record.Stream.Area != FindingAggregate.Area || record.Event.Metadata.IsAudit)
            return ValueTask.CompletedTask;
        var business = FindingEventFacts.Business(record.Event);
        var (tenantId, programId, findingId) = business switch
        {
            FindingRaised ev => (ev.TenantId, ev.ProgramId, ev.FindingId),
            FindingRevised ev => (ev.TenantId, ev.ProgramId, ev.FindingId),
            CorrectiveActionAdded ev => (ev.TenantId, ev.ProgramId, ev.FindingId),
            CorrectiveActionCompleted ev => (ev.TenantId, ev.ProgramId, ev.FindingId),
            FindingAcceptanceLinked ev => (ev.TenantId, ev.ProgramId, ev.FindingId),
            FindingClosed ev => (ev.TenantId, ev.ProgramId, ev.FindingId),
            FindingReopened ev => (ev.TenantId, ev.ProgramId, ev.FindingId),
            FindingHistoryEventImported ev => (ev.TenantId, ev.ProgramId, ev.FindingId),
            _ => throw new InvalidOperationException("A finding work source contains an unsupported fact."),
        };
        if (record.Stream != new FindingAggregate(tenantId, programId, findingId).Stream)
            throw new InvalidOperationException("A finding work fact does not match its owning stream.");
        return record.Event is FindingHistoryEventImported
            ? ValueTask.CompletedTask : projection.ApplyAsync(business, ct);
    }
}
