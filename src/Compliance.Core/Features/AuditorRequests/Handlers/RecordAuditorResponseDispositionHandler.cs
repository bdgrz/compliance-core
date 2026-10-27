using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Records the external disposition of the exact intentionally delivered response version at the revision of that
///     response owner. The disposition is immutable, and a resolution holds the response while it reads it.
/// </summary>
sealed class RecordAuditorResponseDispositionHandler(NativeOperationExecutor operations, IImmutableContentStore content, TimeProvider clock) : IRequestHandler<RecordAuditorResponseDisposition>
{
    const string CommandKind = "RecordAuditorResponseDisposition";

    public async ValueTask<Result> HandleAsync(IRequestContext<RecordAuditorResponseDisposition> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AuditorRequestCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return AuditorCommands.Forbidden("Recording a response disposition requires a personal HTTP actor.");
        if (!AuditorResponseVersionNumber.TryGet(request.Version, out var versionNumber))
            return AuditorCommands.Validation("The audit response version is invalid.");
        if (request.CommandId == Uuid.Empty || request.RequestId == Uuid.Empty || request.ResponseId == Uuid.Empty ||
            request.ExpectedRevision < 0)
            return AuditorCommands.Validation(
                "A disposition requires a command id, a response and a non-negative expected revision.");
        var now = clock.GetUtcNow();
        var snapshot = new AuditorResponse(request.TenantId, request.ProgramId,
            request.RequestId, request.ResponseId, request.CommandId);
        var provenance = AuditorContent.Provenance(request.CommandId, CommandKind, snapshot.Stream,
            AuditorCommands.Actor(actorMemberId, actorDisplay), (ulong)request.ExpectedRevision,
            AuditorCommands.Digest(request), now) with
        { FirstOwner = snapshot.Stream };
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? disposition = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        var outcomeToken = string.Empty;
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(AuditorResponse owner, CancellationToken token)
        {
            var prepared = owner.PrepareDisposition(request.ExpectedRevision, request.Version);
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            if (AuditorRequestRules.Disposition(request.Disposition, request.ExternalReference) is not { } recorded)
                return AuditorCommands.Validation<FirstResponseDescriptor>("The response disposition or external reference is invalid.");
            outcomeToken = recorded.Outcome;
            disposition = await AuditorContent.WriteAsync(content, request.TenantId, AuditorContent.DispositionSchema,
                new AuditorResponseDispositionView(versionNumber, recorded.Outcome, recorded.ExternalReference,
                    actorMemberId, actorDisplay, now), token).ConfigureAwait(false);
            (response, result) = await AuditorContent.UnitAsync(content, request.TenantId, token).ConfigureAwait(false);
            return Result<FirstResponseDescriptor>.Success(response!);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorResponse>(binding,
            () => new AuditorResponse(request.TenantId, request.ProgramId, request.RequestId, request.ResponseId,
                request.CommandId),
            auditorResponse => auditorResponse.Acceptance,
            PrepareAsync,
            (AuditorResponse auditorResponse, ImmutableContentRef frozen) => auditorResponse.RecordDisposition(
                provenance, request.Version, outcomeToken, disposition!, actorMemberId, now, frozen, result!)), context, ct)
            .ConfigureAwait(false);
        return AuditorContent.UnitOutcome(outcome);
    }
}
