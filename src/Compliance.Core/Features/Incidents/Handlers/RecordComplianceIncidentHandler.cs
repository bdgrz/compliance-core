using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

public sealed class RecordComplianceIncidentHandler(IncidentOperations operations) : IRequestHandler<RecordComplianceIncident>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordComplianceIncident> context, CancellationToken ct)
    {
        var request = context.Request;
        if (RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden, "An incident management operation requires a current Bdgrz member.")));
        var (member, display) = ClientServiceActor.Snapshot(context);
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.IncidentId, request.IncidentId,
            nameof(RecordComplianceIncident), null, ActorReference.ForMember(member, display),
            (snapshot, operation, token) => ValueTask.FromResult(IncidentOperations.Outcome(snapshot.Rules.Record(request.Content, member, display, operation.AcceptedAt))), ct);
    }
}
