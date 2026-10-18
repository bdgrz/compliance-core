using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

sealed class ReviseAuditResponseDraftHandler(IAggregateExecutor executor,
    AuditorResponseEvidence evidence, TimeProvider clock) : IRequestHandler<ReviseAuditResponseDraft>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseAuditResponseDraft> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Revising an audit response requires a personal HTTP actor."));
        var validEvidence = await evidence.RequireAvailableAsync(context, request.TenantId,
            request.EvidenceArtifactIds, ct).ConfigureAwait(false);
        if (!validEvidence.IsSuccess)
            return validEvidence;
        return await executor.ExecuteAsync(new AuditorRequest(request.TenantId, request.ProgramId,
                request.RequestId), aggregate =>
            {
                var responseVersion = aggregate.Current?.Responses.FirstOrDefault(response =>
                    response.ResponseId == request.ResponseId)?.CurrentVersion ?? 0;
                var failure = aggregate.ReviseResponseDraft(request.ExpectedRevision,
                    request.ResponseId, responseVersion, request.Summary,
                    request.EvidenceArtifactIds, actorMemberId, actorDisplay, clock.GetUtcNow());
                return CommandFailureRequestAdapter.ToOutcome(failure);
            }, context, ct).ConfigureAwait(false);
    }
}
