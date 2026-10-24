using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Admits one response draft on the open request. The complete normalized draft, the request's review policy and
///     the request generation are frozen on the request before the 202 acknowledgment is frozen on the admission, so an
///     exact retry replays the original acknowledgment even after the request changed; the response owner is then
///     materialized by <c>IndependentChildMaterialization</c>. A request that is resolving or resolved admits nothing.
/// </summary>
sealed class CreateAuditResponseDraftHandler(NativeOperationExecutor operations, IAggregateReader reader,
    AuditorResponseEvidence evidence, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<CreateAuditResponseDraft, OperationAcceptedResponse>
{
    const string CommandKind = "CreateAuditResponseDraft";
    const string ClosedMessage = "A response can only be drafted for an open request.";

    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<CreateAuditResponseDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return AuditorCommands.Forbidden<OperationAcceptedResponse>(
                "Creating an audit response requires a personal HTTP actor.");
        if (request.ResponseId == Uuid.Empty || request.RequestId == Uuid.Empty || request.ExpectedRequestRevision < 0)
            return AuditorCommands.Validation<OperationAcceptedResponse>(
                "A response requires its own identity and a non-negative expected request revision.");
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new AuditorRequest(request.TenantId, request.ProgramId,
            request.RequestId, request.ResponseId), ct).ConfigureAwait(false);
        var provenance = AuditorContent.Provenance(request.ResponseId, CommandKind, snapshot.Stream,
            AuditorCommands.Actor(actorMemberId, actorDisplay), (ulong)request.ExpectedRequestRevision,
            AuditorCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        FirstResponseDescriptor? response = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var terms = await AuditorCommands.TermsAsync(content, request.TenantId, snapshot, ct).ConfigureAwait(false);
            if (!terms.IsSuccess)
                return Result<OperationAcceptedResponse>.Failure(terms.Error!);
            var prepared = snapshot.PrepareWrite(request.ExpectedRequestRevision, ClosedMessage);
            if (!prepared.IsSuccess)
                return Result<OperationAcceptedResponse>.Failure(prepared.Error!);
            var normalized = AuditorRequestRules.Response(request.Summary, request.EvidenceArtifactIds);
            if (normalized is not { } draft)
                return AuditorCommands.Validation<OperationAcceptedResponse>(
                    "A response draft requires a stable identity and bounded content.");
            var available = await evidence.RequireAvailableAsync(context, request.TenantId, draft.EvidenceArtifactIds, ct)
                .ConfigureAwait(false);
            if (!available.IsSuccess)
                return Result<OperationAcceptedResponse>.Failure(available.Error!);
            var authored = await AuditorContent.WriteAsync(content, request.TenantId, AuditorContent.ResponseDraftSchema,
                new AuditorResponseDraftContent(request.ProgramId, request.RequestId, request.ResponseId, draft.Summary,
                    draft.EvidenceArtifactIds, terms.Value!.RequiresInternalReview, actorMemberId, actorDisplay, now), ct)
                .ConfigureAwait(false);
            intent = await AuditorContent.WriteAsync(content, request.TenantId, ChildAdmissionIntent.Schema,
                new ChildAdmissionIntent(AuditorContent.Header(provenance, now), request.RequestId,
                    (ulong)snapshot.Generation, AdmittedChildKind.AuditorResponse, request.ResponseId, authored,
                    await AuditorContent.EmptyProofsAsync(content, request.TenantId, request.ResponseId, ct)
                        .ConfigureAwait(false), (ulong)request.ExpectedRequestRevision + 1), ct).ConfigureAwait(false);
            response = await AuditorContent.AcceptedResponseAsync(content, request.TenantId, request.ResponseId, now, ct)
                .ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorRequest>(binding,
            () => new AuditorRequest(request.TenantId, request.ProgramId, request.RequestId, request.ResponseId),
            auditorRequest => auditorRequest.Acceptance,
            auditorRequest => AuditorContent.Prepared(
                auditorRequest.PrepareWrite(request.ExpectedRequestRevision, ClosedMessage), response),
            (AuditorRequest auditorRequest, ImmutableContentRef acknowledgment) =>
                auditorRequest.AdmitResponse(provenance, intent!, acknowledgment)), context, ct).ConfigureAwait(false);
        return await AuditorContent.ResponseAsync<OperationAcceptedResponse>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }
}
