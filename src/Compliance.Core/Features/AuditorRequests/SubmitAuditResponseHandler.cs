using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Submits the exact current draft version of a response at the revision of that response owner.</summary>
sealed class SubmitAuditResponseHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<SubmitAuditResponse>
{
    const string CommandKind = "SubmitAuditResponse";

    public async ValueTask<Result> HandleAsync(IRequestContext<SubmitAuditResponse> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return AuditorCommands.Forbidden("Submitting an audit response requires a personal HTTP actor.");
        if (request.CommandId == Uuid.Empty || request.RequestId == Uuid.Empty || request.ResponseId == Uuid.Empty ||
            request.ExpectedRevision < 0 || !AuditorResponseVersionNumber.TryGet(request.Version, out var versionNumber))
            return AuditorCommands.Validation(
                "A submission requires a command id, a response, a positive version and a non-negative expected revision.");
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new AuditorResponse(request.TenantId, request.ProgramId,
            request.RequestId, request.ResponseId, request.CommandId), ct).ConfigureAwait(false);
        var provenance = AuditorContent.Provenance(request.CommandId, CommandKind, snapshot.Stream,
            AuditorCommands.Actor(actorMemberId, actorDisplay), (ulong)request.ExpectedRevision,
            AuditorCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? submission = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = snapshot.PrepareSubmit(request.ExpectedRevision, request.Version);
            if (!prepared.IsSuccess)
                return prepared;
            if (AuditorRequestRules.SubmissionNote(request.SubmissionNote) is not { } note)
                return AuditorCommands.Validation("Only a draft with a bounded submission note can be submitted.");
            submission = await AuditorContent.WriteAsync(content, request.TenantId, AuditorContent.SubmissionSchema,
                new AuditorResponseSubmissionView(versionNumber, note, actorMemberId, actorDisplay, now), ct)
                .ConfigureAwait(false);
            (response, result) = await AuditorContent.UnitAsync(content, request.TenantId, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorResponse>(binding,
            () => new AuditorResponse(request.TenantId, request.ProgramId, request.RequestId, request.ResponseId,
                request.CommandId),
            auditorResponse => auditorResponse.Acceptance,
            auditorResponse => AuditorContent.Prepared(
                auditorResponse.PrepareSubmit(request.ExpectedRevision, request.Version), response),
            (AuditorResponse auditorResponse, ImmutableContentRef frozen) => auditorResponse.Submit(provenance,
                request.Version, submission!, actorMemberId, now, frozen, result!)), context, ct).ConfigureAwait(false);
        return AuditorContent.UnitOutcome(outcome);
    }
}
