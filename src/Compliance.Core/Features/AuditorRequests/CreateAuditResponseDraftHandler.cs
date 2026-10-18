using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

sealed class CreateAuditResponseDraftHandler(IAggregateExecutor executor,
    AuditorResponseEvidence evidence, TimeProvider clock) : IRequestHandler<CreateAuditResponseDraft>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<CreateAuditResponseDraft> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Creating an audit response requires a personal HTTP actor."));
        var validEvidence = await evidence.RequireAvailableAsync(context, request.TenantId,
            request.EvidenceArtifactIds, ct).ConfigureAwait(false);
        if (!validEvidence.IsSuccess)
            return validEvidence;
        return await executor.ExecuteAsync(new AuditorRequest(request.TenantId, request.ProgramId,
                request.RequestId), aggregate => CommandFailureRequestAdapter.ToOutcome(
                aggregate.CreateResponseDraft(aggregate.Revision, request.ResponseId,
                    request.Summary, request.EvidenceArtifactIds, actorMemberId, actorDisplay,
                    clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
