using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Revises the wording, scope, priority and due date of an open request at its owner revision. The complete terms
///     after the revision are frozen as verified content; an exact retry replays the original Unit result.
/// </summary>
public sealed class ReviseAuditorRequestHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<ReviseAuditorRequest>
{
    const string CommandKind = "ReviseAuditorRequest";
    const string ResolvedMessage = "A resolved auditor request must be reopened before revision.";

    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseAuditorRequest> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return AuditorCommands.Forbidden("Revising an auditor request requires a personal HTTP actor.");
        if (request.CommandId == Uuid.Empty || request.RequestId == Uuid.Empty || request.ExpectedRevision < 0)
            return AuditorCommands.Validation("A revision requires a command id and a non-negative expected revision.");
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new AuditorRequest(request.TenantId, request.ProgramId,
            request.RequestId, request.CommandId), ct).ConfigureAwait(false);
        var provenance = AuditorContent.Provenance(request.CommandId, CommandKind, snapshot.Stream,
            AuditorCommands.Actor(actorMemberId, actorDisplay), (ulong)request.ExpectedRevision,
            AuditorCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? terms = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var current = await AuditorCommands.TermsAsync(content, request.TenantId, snapshot, ct).ConfigureAwait(false);
            if (!current.IsSuccess)
                return Result.Failure(current.Error!);
            var prepared = snapshot.PrepareWrite(request.ExpectedRevision, ResolvedMessage);
            if (!prepared.IsSuccess)
                return prepared;
            var revised = AuditorRequestRules.ReviseTerms(current.Value!, request, actorMemberId, actorDisplay, now);
            if (!revised.IsSuccess)
                return Result.Failure(revised.Error!);
            terms = await AuditorContent.WriteAsync(content, request.TenantId, AuditorContent.TermsSchema, revised.Value!, ct).ConfigureAwait(false);
            (response, result) = await AuditorContent.UnitAsync(content, request.TenantId, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorRequest>(binding,
            () => new AuditorRequest(request.TenantId, request.ProgramId, request.RequestId, request.CommandId),
            auditorRequest => auditorRequest.Acceptance,
            auditorRequest => AuditorContent.Prepared(
                auditorRequest.PrepareWrite(request.ExpectedRevision, ResolvedMessage), response),
            (AuditorRequest auditorRequest, ImmutableContentRef frozen) =>
                auditorRequest.Revise(provenance, terms!, actorMemberId, now, frozen, result!)), context, ct)
            .ConfigureAwait(false);
        return AuditorContent.UnitOutcome(outcome);
    }
}
