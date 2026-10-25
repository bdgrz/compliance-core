using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

public sealed class ReviseComplianceIncidentHandler(IncidentOperations operations) : IRequestHandler<ReviseComplianceIncident>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseComplianceIncident> context, CancellationToken ct)
    {
        var request = context.Request;
        if (RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden, "An incident management operation requires a current Bdgrz member.")));
        var (member, display) = ClientServiceActor.Snapshot(context);
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.IncidentId, request.CommandId,
            nameof(ReviseComplianceIncident), request.ExpectedRevision, ActorReference.ForMember(member, display),
            (snapshot, operation, token) => ValueTask.FromResult(IncidentOperations.Outcome(snapshot.Rules.Revise(request.ExpectedRevision, request.Content, request.ChangeReason, member, display, operation.AcceptedAt))), ct);
    }
}
