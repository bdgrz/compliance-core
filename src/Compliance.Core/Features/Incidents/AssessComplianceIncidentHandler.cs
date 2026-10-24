using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

public sealed class AssessComplianceIncidentHandler(NativeOperationExecutor operations,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<AssessComplianceIncident>
{
    const string CommandKind = "AssessComplianceIncident";

    public async ValueTask<Result> HandleAsync(IRequestContext<AssessComplianceIncident> context,
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
        var response = await IncidentOperations.UnitResponseAsync(content, request.TenantId, ct).ConfigureAwait(false);
        var accepted = await operations.ExecuteAsync(new NativeOperation<ComplianceIncident>(
            AcceptedOperationBinding.Of(provenance),
            () => IncidentOperations.Owner(request.TenantId, request.ProgramId, request.IncidentId, request.CommandId),
            incident => incident.Acceptance,
            incident => IncidentOperations.Prepared(incident.PrepareAssess(request.ExpectedRevision, request.Content,
                memberId, display, provenance.AcceptedAt), response),
            (ComplianceIncident incident, ImmutableContentRef frozen) => IncidentOperations.Applied(
                incident.Assess(provenance, frozen, request.ExpectedRevision, request.Content, memberId, display))),
            context, ct).ConfigureAwait(false);
        return IncidentOperations.Done(accepted);
    }
}
