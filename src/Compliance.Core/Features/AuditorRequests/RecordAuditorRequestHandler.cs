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
    IDomainEventReader events,
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
        var address = new AuditorRequest(request.TenantId, request.ProgramId, request.RequestId, request.RequestId);
        var provenance = AuditorContent.Provenance(request.RequestId, CommandKind, address.Stream,
            AuditorCommands.Actor(actorMemberId, actorDisplay), null, AuditorCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? terms = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorRequest>(binding,
            () => new AuditorRequest(request.TenantId, request.ProgramId, request.RequestId, request.RequestId),
            auditorRequest => auditorRequest.Acceptance,
            async (snapshot, prepareCt) =>
            {
                _ = await AuditorRequestHistory.ReadAsync(events, snapshot, prepareCt).ConfigureAwait(false);
                var prepared = snapshot.PrepareRecord();
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                var normalized = AuditorRequestRules.RecordTerms(request, actorMemberId, actorDisplay, now);
                if (!normalized.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(normalized.Error!);
                if (!await AuditorRequestOwner.IsActiveClientMemberAsync(reader, request.TenantId, request.OwnerMemberId, prepareCt)
                    .ConfigureAwait(false))
                    return Result<FirstResponseDescriptor>.Failure(AuditorRequestOwner.InvalidOwner());
                terms = await AuditorContent.WriteAsync(content, request.TenantId, AuditorContent.TermsSchema, normalized.Value!, prepareCt).ConfigureAwait(false);
                (response, result) = await AuditorContent.UnitAsync(content, request.TenantId, prepareCt).ConfigureAwait(false);
                return Result<FirstResponseDescriptor>.Success(response!);
            },
            (AuditorRequest auditorRequest, ImmutableContentRef frozen) =>
                auditorRequest.Record(provenance, terms!, actorMemberId, now, frozen, result!)), context, ct)
            .ConfigureAwait(false);
        return AuditorContent.UnitOutcome(outcome);
    }
}
