using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Records one auditor request. The request id is the logical operation, so an exact retry replays the original Unit
///     result after any later change and a changed retry conflicts; the owner of the request must be an active client
///     member when the request is first recorded.
/// </summary>
public sealed class RecordAuditorRequestHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<RecordAuditorRequest>
{
    const string CommandKind = "RecordAuditorRequest";

    public async ValueTask<Result> HandleAsync(IRequestContext<RecordAuditorRequest> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return AuditorCommands.Forbidden("Recording an auditor request requires a personal HTTP actor.");
        if (request.RequestId == Uuid.Empty || request.ProgramId == Uuid.Empty)
            return AuditorCommands.Validation("An auditor request requires its own identity and program.");
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new AuditorRequest(request.TenantId, request.ProgramId,
            request.RequestId, request.RequestId), ct).ConfigureAwait(false);
        var provenance = AuditorContent.Provenance(request.RequestId, CommandKind, snapshot.Stream,
            AuditorCommands.Actor(actorMemberId, actorDisplay), null, AuditorCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? terms = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = snapshot.PrepareRecord();
            if (!prepared.IsSuccess)
                return prepared;
            var normalized = AuditorRequestRules.RecordTerms(request, actorMemberId, actorDisplay, now);
            if (!normalized.IsSuccess)
                return Result.Failure(normalized.Error!);
            if (!await AuditorRequestOwner.IsActiveClientMemberAsync(reader, request.TenantId, request.OwnerMemberId, ct)
                    .ConfigureAwait(false))
                return Result.Failure(AuditorRequestOwner.InvalidOwner());
            terms = await AuditorContent.WriteAsync(content, request.TenantId, AuditorContent.TermsSchema, normalized.Value!, ct).ConfigureAwait(false);
            (response, result) = await AuditorContent.UnitAsync(content, request.TenantId, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorRequest>(binding,
            () => new AuditorRequest(request.TenantId, request.ProgramId, request.RequestId, request.RequestId),
            auditorRequest => auditorRequest.Acceptance,
            auditorRequest => AuditorContent.Prepared(auditorRequest.PrepareRecord(), response),
            (AuditorRequest auditorRequest, ImmutableContentRef frozen) =>
                auditorRequest.Record(provenance, terms!, actorMemberId, now, frozen, result!)), context, ct)
            .ConfigureAwait(false);
        return AuditorContent.UnitOutcome(outcome);
    }
}
