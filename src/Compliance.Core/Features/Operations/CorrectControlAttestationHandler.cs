using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records a new attestation version superseding the latest; HTTP-only.</summary>
public sealed class CorrectControlAttestationHandler(NativeOperationExecutor operations,
    ControlOperationsReader operatingReader, IAggregateReader reader, ControlReader controlSources, OperatingAuthority authority,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<CorrectControlAttestation, ControlOccurrenceView>
{
    public ValueTask<Result<ControlOccurrenceView>> HandleAsync(
        IRequestContext<CorrectControlAttestation> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<ControlOccurrenceView>> HandleCoreAsync(
        IRequestContext<CorrectControlAttestation> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result<ControlOccurrenceView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Occurrence proofs require personal HTTP submission."));
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result<ControlOccurrenceView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A correction requires a command id and a non-negative expected revision."));
        var now = clock.GetUtcNow();
        var prepared = await OccurrenceAttestation.PrepareAsync(controlSources, operatingReader, authority, request.TenantId,
            request.ProgramId, request.ControlId, request.OccurrenceId, request.PerformedByPersonId, context.Actor, now,
            ct).ConfigureAwait(false);
        if (!prepared.IsSuccess)
            return Result<ControlOccurrenceView>.Failure(prepared.Error!);
        var input = new AttestationInput(request.Result, request.PerformedAt, request.CoveredFrom,
            request.CoveredUntil, request.Notes, request.Rationale, request.Evidence ?? [], prepared.Value!.Performer);
        return await OccurrenceProofs.AttestAsync(operations, reader, content, context, "CorrectControlAttestation",
            request.CommandId, request.ExpectedRevision, AuthoredIntentDigest.Compute(request), prepared.Value, input,
            request.CorrectionReason ?? string.Empty, now, ct).ConfigureAwait(false);
    }
}
