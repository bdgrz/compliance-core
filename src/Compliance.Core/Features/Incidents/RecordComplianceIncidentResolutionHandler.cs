using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

public sealed class RecordComplianceIncidentResolutionHandler(IAggregateExecutor executor,
    TimeProvider clock) : IRequestHandler<RecordComplianceIncidentResolution>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordComplianceIncidentResolution> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var (actorMemberId, actorDisplay) = ClientServiceActor.Snapshot(context);
        return executor.ExecuteAsync(new ComplianceIncident(request.TenantId, request.ProgramId,
                request.IncidentId), incident => CommandFailureRequestAdapter.ToOutcome(
                incident.Resolve(request.ExpectedRevision, request.Content, actorMemberId,
                    actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
