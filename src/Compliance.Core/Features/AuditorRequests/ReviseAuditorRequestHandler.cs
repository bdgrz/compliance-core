using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Revises the wording, scope, priority and due date of an open request at its owner revision. The complete terms
///     after the revision are frozen as verified content; an exact retry replays the original Unit result.
/// </summary>
public sealed class ReviseAuditorRequestHandler(NativeOperationExecutor operations, IDomainEventReader events,
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
        var address = new AuditorRequest(request.TenantId, request.ProgramId, request.RequestId, request.CommandId);
        var provenance = AuditorContent.Provenance(request.CommandId, CommandKind, address.Stream,
            AuditorCommands.Actor(actorMemberId, actorDisplay), (ulong)request.ExpectedRevision,
            AuditorCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? terms = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorRequest>(binding,
            () => new AuditorRequest(request.TenantId, request.ProgramId, request.RequestId, request.CommandId),
            auditorRequest => auditorRequest.Acceptance,
            async (snapshot, prepareCt) =>
            {
                _ = await AuditorRequestHistory.ReadAsync(events, snapshot, prepareCt).ConfigureAwait(false);
                var current = await AuditorCommands.TermsAsync(content, request.TenantId, snapshot, prepareCt).ConfigureAwait(false);
                if (!current.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(current.Error!);
                var prepared = snapshot.PrepareWrite(request.ExpectedRevision, ResolvedMessage);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                var revised = AuditorRequestRules.ReviseTerms(current.Value!, request, actorMemberId, actorDisplay, now);
                if (!revised.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(revised.Error!);
                terms = await AuditorContent.WriteAsync(content, request.TenantId, AuditorContent.TermsSchema, revised.Value!, prepareCt).ConfigureAwait(false);
                (response, result) = await AuditorContent.UnitAsync(content, request.TenantId, prepareCt).ConfigureAwait(false);
                return Result<FirstResponseDescriptor>.Success(response!);
            },
            (AuditorRequest auditorRequest, ImmutableContentRef frozen) =>
                auditorRequest.Revise(provenance, terms!, actorMemberId, now, frozen, result!)), context, ct)
            .ConfigureAwait(false);
        return AuditorContent.UnitOutcome(outcome);
    }
}
