using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records the performer's own attestation; HTTP-only, never an MCP tool.</summary>
public sealed class AttestControlOccurrenceHandler(NativeOperationExecutor operations,
    ControlOperationsReader operatingReader, IAggregateReader reader, OperatingAuthority authority,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<AttestControlOccurrence, ControlOccurrenceView>
{
    public async ValueTask<Result<ControlOccurrenceView>> HandleAsync(
        IRequestContext<AttestControlOccurrence> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result<ControlOccurrenceView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Occurrence proofs require personal HTTP submission."));
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result<ControlOccurrenceView>.Failure(new RequestError(RequestErrorKind.Validation,
                "An attestation requires a command id and a non-negative expected revision."));
        var now = clock.GetUtcNow();
        var prepared = await OccurrenceAttestation.PrepareAsync(reader, operatingReader, authority, request.TenantId,
            request.ProgramId, request.ControlId, request.OccurrenceId, request.PerformedByPersonId, context.Actor, now,
            ct).ConfigureAwait(false);
        if (!prepared.IsSuccess)
            return Result<ControlOccurrenceView>.Failure(prepared.Error!);
        var input = new AttestationInput(request.Result, request.PerformedAt, request.CoveredFrom,
            request.CoveredUntil, request.Notes, request.Rationale, request.Evidence ?? [], prepared.Value!.Performer);
        return await OccurrenceProofs.AttestAsync(operations, reader, content, context, "AttestControlOccurrence",
            request.CommandId, request.ExpectedRevision, AuthoredIntentDigest.Compute(request), prepared.Value, input,
            null, now, ct).ConfigureAwait(false);
    }
}
