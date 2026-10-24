using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Reopens a resolved request at its owner revision. The generation advances; all historical responses are retained,
///     and an exact retry of the old resolution returns its old result without closing the new generation.
/// </summary>
public sealed class ReopenAuditorRequestHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<ReopenAuditorRequest>
{
    const string CommandKind = "ReopenAuditorRequest";

    public async ValueTask<Result> HandleAsync(IRequestContext<ReopenAuditorRequest> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return AuditorCommands.Forbidden("Reopening an auditor request requires a personal HTTP actor.");
        if (request.CommandId == Uuid.Empty || request.RequestId == Uuid.Empty || request.ExpectedRevision < 0)
            return AuditorCommands.Validation("A reopening requires a command id and a non-negative expected revision.");
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new AuditorRequest(request.TenantId, request.ProgramId,
            request.RequestId, request.CommandId), ct).ConfigureAwait(false);
        var provenance = AuditorContent.Provenance(request.CommandId, CommandKind, snapshot.Stream,
            AuditorCommands.Actor(actorMemberId, actorDisplay), (ulong)request.ExpectedRevision,
            AuditorCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? reopen = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var scoped = await AuditorCommands.TermsAsync(content, request.TenantId, snapshot, ct).ConfigureAwait(false);
            if (!scoped.IsSuccess)
                return Result.Failure(scoped.Error!);
            var prepared = snapshot.PrepareReopen(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return prepared;
            var reason = AuditorRequestRules.ReopenReason(request.ReopenReason);
            if (reason is null)
                return AuditorCommands.Validation("Reopening a request needs a bounded explanation.");
            reopen = await AuditorContent.WriteAsync(content, request.TenantId, AuditorContent.ReopenSchema, new AuditorRequestReopenContent(reason, actorMemberId, actorDisplay, now), ct)
                .ConfigureAwait(false);
            (response, result) = await AuditorContent.UnitAsync(content, request.TenantId, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorRequest>(binding,
            () => new AuditorRequest(request.TenantId, request.ProgramId, request.RequestId, request.CommandId),
            auditorRequest => auditorRequest.Acceptance,
            auditorRequest => AuditorContent.Prepared(auditorRequest.PrepareReopen(request.ExpectedRevision), response),
            (AuditorRequest auditorRequest, ImmutableContentRef frozen) =>
                auditorRequest.Reopen(provenance, reopen!, actorMemberId, now, frozen, result!)), context, ct)
            .ConfigureAwait(false);
        return AuditorContent.UnitOutcome(outcome);
    }
}
