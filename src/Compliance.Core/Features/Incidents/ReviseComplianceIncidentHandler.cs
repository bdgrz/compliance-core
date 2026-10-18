using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

public sealed class ReviseComplianceIncidentHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReviseComplianceIncident>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseComplianceIncident> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var (actorMemberId, actorDisplay) = ClientServiceActor.Snapshot(context);
        return executor.ExecuteAsync(new ComplianceIncident(request.TenantId, request.ProgramId,
                request.IncidentId), incident => CommandFailureRequestAdapter.ToOutcome(
                incident.Revise(request.ExpectedRevision, request.Content, request.ChangeReason,
                    actorMemberId, actorDisplay, clock.GetUtcNow())), context, ct);
    }
}
