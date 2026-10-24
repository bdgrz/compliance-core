using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

public sealed class RecordComplianceIncidentResolutionHandler(NativeOperationExecutor operations,
    FollowUpCompletionVerifier completions, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RecordComplianceIncidentResolution>
{
    const string CommandKind = "RecordComplianceIncidentResolution";

    public async ValueTask<Result> HandleAsync(IRequestContext<RecordComplianceIncidentResolution> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (IncidentOperations.Validate(request.CommandId, request.TenantId, request.ProgramId, request.IncidentId,
                request.ExpectedRevision) is { } invalid)
            return Result.Failure(invalid);
        if (request.Content is null)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The resolution is required."));
        var (actor, forbidden) = IncidentOperations.ActorOf(context.Actor);
        if (forbidden is not null)
            return Result.Failure(forbidden);
        var (memberId, display) = ClientServiceActor.Snapshot(context);
        var provenance = IncidentOperations.Provenance(request, CommandKind, request.CommandId, request.TenantId,
            request.ProgramId, request.IncidentId, request.ExpectedRevision, actor!, clock.GetUtcNow());
        VerifiedIncidentResolution? resolution = null;
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(ComplianceIncident incident, CancellationToken token)
        {
            var verified = await completions.VerifyAsync(request.TenantId, request.ProgramId, request.Content, token)
                .ConfigureAwait(false);
            if (!verified.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(verified.Error!);
            resolution = verified.Value!;
            return await IncidentOperations.PrepareAsync(incident.PrepareResolve(request.ExpectedRevision, resolution,
                memberId, display, provenance.AcceptedAt), content, request.TenantId, token).ConfigureAwait(false);
        }
        var accepted = await operations.ExecuteAsync(new NativeOperation<ComplianceIncident>(
            AcceptedOperationBinding.Of(provenance),
            () => IncidentOperations.Owner(request.TenantId, request.ProgramId, request.IncidentId, request.CommandId),
            incident => incident.Acceptance,
            PrepareAsync,
            (ComplianceIncident incident, ImmutableContentRef frozen) => IncidentOperations.Applied(
                incident.Resolve(provenance, frozen, request.ExpectedRevision, resolution!, memberId, display))),
            context, ct).ConfigureAwait(false);
        return IncidentOperations.Done(accepted);
    }
}
