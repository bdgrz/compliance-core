using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Operations.Handlers;

/// <summary>Records a new attestation version superseding the latest; HTTP-only.</summary>
public sealed class CorrectControlAttestationHandler(NativeOperationExecutor operations,
    ControlOperationsReader operatingReader, ControlReader controlSources, OperatingAuthority authority,
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
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        return await OccurrenceProofs.AttestAsync(operations, content, context, "CorrectControlAttestation", request.CommandId,
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), request.TenantId, request.ProgramId,
            request.ControlId, request.OccurrenceId, actor,
            token => OccurrenceAttestation.PrepareAsync(controlSources, operatingReader, authority, request.TenantId,
                request.ProgramId, request.ControlId, request.OccurrenceId, request.PerformedByPersonId,
                context.Actor, now, token),
            prepared => new AttestationInput(request.Result, request.PerformedAt, request.CoveredFrom,
                request.CoveredUntil, request.Notes, request.Rationale, request.Evidence ?? [], prepared.Performer),
            request.CorrectionReason ?? string.Empty, now, ct).ConfigureAwait(false);
    }
}
