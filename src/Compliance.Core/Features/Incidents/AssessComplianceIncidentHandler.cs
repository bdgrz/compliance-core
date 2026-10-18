using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

public sealed class AssessComplianceIncidentHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<AssessComplianceIncident>
{
    public ValueTask<Result> HandleAsync(IRequestContext<AssessComplianceIncident> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var (actorMemberId, actorDisplay) = ClientServiceActor.Snapshot(context);
        return executor.ExecuteAsync(new ComplianceIncident(request.TenantId, request.ProgramId,
                request.IncidentId), incident => CommandFailureRequestAdapter.ToOutcome(
                incident.Assess(request.ExpectedRevision, request.Content, actorMemberId,
                    actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
