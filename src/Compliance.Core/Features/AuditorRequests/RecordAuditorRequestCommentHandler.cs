using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Admits one comment on the open request. The complete normalized body, compartment, original actor and the request
///     generation are frozen on the request before the 202 acknowledgment, and the comment's own thread is materialized
///     independently, so an exact retry never repeats a comment and a later resolution cannot discard it.
/// </summary>
public sealed class RecordAuditorRequestCommentHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RecordAuditorRequestComment, OperationAcceptedResponse>
{
    const string CommandKind = "RecordAuditorRequestComment";
    const string ClosedMessage = "Discussion can only be added to an open request.";

    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<RecordAuditorRequestComment> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return AuditorCommands.Forbidden<OperationAcceptedResponse>(
                "Recording an auditor request comment requires a personal HTTP actor.");
        if (request.CommentId == Uuid.Empty || request.RequestId == Uuid.Empty || request.ExpectedRequestRevision < 0)
            return AuditorCommands.Validation<OperationAcceptedResponse>(
                "A comment requires its own identity and a non-negative expected request revision.");
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new AuditorRequest(request.TenantId, request.ProgramId,
            request.RequestId, request.CommentId), ct).ConfigureAwait(false);
        var provenance = AuditorContent.Provenance(request.CommentId, CommandKind, snapshot.Stream,
            AuditorCommands.Actor(actorMemberId, actorDisplay), (ulong)request.ExpectedRequestRevision,
            AuditorCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        FirstResponseDescriptor? response = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var scoped = await AuditorCommands.TermsAsync(content, request.TenantId, snapshot, ct).ConfigureAwait(false);
            if (!scoped.IsSuccess)
                return Result<OperationAcceptedResponse>.Failure(scoped.Error!);
            var prepared = snapshot.PrepareWrite(request.ExpectedRequestRevision, ClosedMessage);
            if (!prepared.IsSuccess)
                return Result<OperationAcceptedResponse>.Failure(prepared.Error!);
            if (AuditorRequestRules.Comment(request.Body, request.Compartment) is not { } comment)
                return AuditorCommands.Validation<OperationAcceptedResponse>(
                    "A request comment needs bounded text and a supported compartment.");
            var authored = await AuditorContent.WriteAsync(content, request.TenantId, AuditorContent.CommentSchema,
                new AuditorCommentContent(request.ProgramId, request.RequestId, request.CommentId, comment.Body,
                    comment.Compartment, actorMemberId, actorDisplay, now), ct).ConfigureAwait(false);
            intent = await AuditorContent.WriteAsync(content, request.TenantId, ChildAdmissionIntent.Schema,
                new ChildAdmissionIntent(AuditorContent.Header(provenance, now), request.RequestId,
                    (ulong)snapshot.Generation, AdmittedChildKind.AuditorComment, request.CommentId, authored,
                    await AuditorContent.EmptyProofsAsync(content, request.TenantId, request.CommentId, ct)
                        .ConfigureAwait(false), (ulong)request.ExpectedRequestRevision + 1), ct).ConfigureAwait(false);
            response = await AuditorContent.AcceptedResponseAsync(content, request.TenantId, request.CommentId, now, ct)
                .ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorRequest>(binding,
            () => new AuditorRequest(request.TenantId, request.ProgramId, request.RequestId, request.CommentId),
            auditorRequest => auditorRequest.Acceptance,
            auditorRequest => AuditorContent.Prepared(
                auditorRequest.PrepareWrite(request.ExpectedRequestRevision, ClosedMessage), response),
            (AuditorRequest auditorRequest, ImmutableContentRef acknowledgment) =>
                auditorRequest.AdmitComment(provenance, intent!, acknowledgment)), context, ct).ConfigureAwait(false);
        return await AuditorContent.ResponseAsync<OperationAcceptedResponse>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }
}
