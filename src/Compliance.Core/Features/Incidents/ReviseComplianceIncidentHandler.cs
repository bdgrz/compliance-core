using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

public sealed class ReviseComplianceIncidentHandler(NativeOperationExecutor operations,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<ReviseComplianceIncident>
{
    const string CommandKind = "ReviseComplianceIncident";

    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseComplianceIncident> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (IncidentOperations.Validate(request.CommandId, request.TenantId, request.ProgramId, request.IncidentId,
                request.ExpectedRevision) is { } invalid)
            return Result.Failure(invalid);
        var (actor, forbidden) = IncidentOperations.ActorOf(context.Actor);
        if (forbidden is not null)
            return Result.Failure(forbidden);
        var (memberId, display) = ClientServiceActor.Snapshot(context);
        var provenance = IncidentOperations.Provenance(request, CommandKind, request.CommandId, request.TenantId,
            request.ProgramId, request.IncidentId, request.ExpectedRevision, actor!, clock.GetUtcNow());
        var accepted = await operations.ExecuteAsync(new NativeOperation<ComplianceIncident>(
            AcceptedOperationBinding.Of(provenance),
            () => IncidentOperations.Owner(request.TenantId, request.ProgramId, request.IncidentId, request.CommandId),
            incident => incident.Acceptance,
            (incident, token) => IncidentOperations.PrepareAsync(incident.PrepareRevise(request.ExpectedRevision,
                request.Content, request.ChangeReason, memberId, display, provenance.AcceptedAt), content, request.TenantId, token),
            (ComplianceIncident incident, ImmutableContentRef frozen) => IncidentOperations.Applied(
                incident.Revise(provenance, frozen, request.ExpectedRevision, request.Content, request.ChangeReason,
                    memberId, display))), context, ct).ConfigureAwait(false);
        return IncidentOperations.Done(accepted);
    }
}
