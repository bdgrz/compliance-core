using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Incidents;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents.Handlers;

public sealed class AssessComplianceIncidentHandler(IncidentOperations operations) : IRequestHandler<AssessComplianceIncident>
{
    public ValueTask<Result> HandleAsync(IRequestContext<AssessComplianceIncident> context, CancellationToken ct)
    {
        var request = context.Request;
        if (RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden, "An incident management operation requires a current Bdgrz member.")));
        var (member, display) = ClientServiceActor.Snapshot(context);
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.IncidentId, request.CommandId,
            nameof(AssessComplianceIncident), request.ExpectedRevision, ActorReference.ForMember(member, display),
            (snapshot, operation, token) => ValueTask.FromResult(IncidentOperations.Outcome(snapshot.Rules.Assess(request.ExpectedRevision, request.Content, member, display, operation.AcceptedAt))), ct);
    }
}
