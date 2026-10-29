using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.History;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AuditorRequests.Handlers;

/// <summary>
///     Admits one comment on the open request. The complete normalized body, compartment, original actor and the request
///     generation are frozen on the request before the 202 acknowledgment, and the comment's own thread is materialized
///     independently, so an exact retry never repeats a comment and a later resolution cannot discard it.
/// </summary>
public sealed class RecordAuditorRequestCommentHandler(NativeOperationExecutor operations, IDomainEventReader events,
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
        var address = new AuditorRequest(request.TenantId, request.ProgramId, request.RequestId, request.CommentId);
        var provenance = AuditorContent.Provenance(request.CommentId, CommandKind, address.Stream,
            AuditorCommands.Actor(actorMemberId, actorDisplay), (ulong)request.ExpectedRequestRevision,
            AuditorCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        FirstResponseDescriptor? response = null;
        AuditorRequestPreparedCut? preparedCut = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorRequest>(binding,
            () => new AuditorRequest(request.TenantId, request.ProgramId, request.RequestId, request.CommentId),
            auditorRequest => auditorRequest.Acceptance,
            async (snapshot, prepareCt) =>
            {
                _ = await AuditorRequestHistory.ReadAsync(events, snapshot, prepareCt).ConfigureAwait(false);
                var scoped = await AuditorCommands.TermsAsync(content, request.TenantId, snapshot, prepareCt).ConfigureAwait(false);
                if (!scoped.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(scoped.Error!);
                var prepared = snapshot.PrepareWrite(request.ExpectedRequestRevision, ClosedMessage);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                if (AuditorRequestRules.Comment(request.Body, request.Compartment) is not { } comment)
                    return AuditorCommands.Validation<FirstResponseDescriptor>(
                        "A request comment needs bounded text and a supported compartment.");
                var authored = await AuditorContent.WriteAsync(content, request.TenantId, AuditorContent.CommentSchema,
                new AuditorCommentContent(request.ProgramId, request.RequestId, request.CommentId, comment.Body,
                    comment.Compartment, actorMemberId, actorDisplay, now), prepareCt).ConfigureAwait(false);
                intent = await AuditorContent.WriteAsync(content, request.TenantId, ChildAdmissionIntent.Schema,
                new ChildAdmissionIntent(AuditorContent.Header(provenance, now), request.RequestId,
                    (ulong)snapshot.Generation, AdmittedChildKind.AuditorComment, request.CommentId, authored,
                    await AuditorContent.EmptyProofsAsync(content, request.TenantId, request.CommentId, prepareCt)
                        .ConfigureAwait(false), (ulong)request.ExpectedRequestRevision + 1), prepareCt).ConfigureAwait(false);
                response = await AuditorContent.AcceptedResponseAsync(content, request.TenantId, request.CommentId, now, prepareCt)
                .ConfigureAwait(false);
                preparedCut = AuditorRequestPreparedCut.Capture(snapshot);
                return Result<FirstResponseDescriptor>.Success(response!);
            },
            (AuditorRequest auditorRequest, ImmutableContentRef acknowledgment) =>
            {
                if (preparedCut?.Matches(auditorRequest) != true)
                    return AuditorRequestPreparedCut.Changed();
                return auditorRequest.AdmitComment(provenance, intent!, acknowledgment);
            }), context, ct).ConfigureAwait(false);
        return await AuditorContent.ResponseAsync<OperationAcceptedResponse>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }
}
