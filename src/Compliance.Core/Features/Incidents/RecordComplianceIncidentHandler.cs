using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

public sealed class RecordComplianceIncidentHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RecordComplianceIncident>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordComplianceIncident> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var (actorMemberId, actorDisplay) = ClientServiceActor.Snapshot(context);
        return executor.ExecuteAsync(new ComplianceIncident(request.TenantId, request.ProgramId,
                request.IncidentId), incident => CommandFailureRequestAdapter.ToOutcome(
                incident.Record(request.Content, actorMemberId, actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
