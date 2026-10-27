using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Handlers;

/// <summary>
///     Cancels a pending amendment. The cancellation fact is appended on the period owner, which is the local
///     receiver fence: a delayed <c>ApplyTypeIIPeriodRange</c> sees it and does nothing. Only afterwards does the
///     release reactor settle the pending calendar reservation, so the old range stays held throughout. An amendment
///     that already applied is irreversible; the cancellation conflicts and the applied range recovers forward.
/// </summary>
public sealed class CancelTypeIIPeriodAmendmentHandler(NativeOperationExecutor operations,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<CancelTypeIIPeriodAmendment, UnitResult>
{
    const string CommandKind = "CancelTypeIIPeriodAmendment";
    const string InitialEpisode = "initial";
    const int MaximumReasonLength = 2000;

    public async ValueTask<Result<UnitResult>> HandleAsync(IRequestContext<CancelTypeIIPeriodAmendment> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Canceling a Type II period amendment requires a personal HTTP actor."));
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.PeriodId == Uuid.Empty || request.AmendmentId == Uuid.Empty)
            return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Validation,
                "A command id, tenant, program, period and amendment are required."));
        if (request.ExpectedRevision < 0 || string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Trim().Length > MaximumReasonLength)
            return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Validation,
                "A cancellation requires a non-negative expected revision and a bounded reason."));

        var realm = request.TenantId.ToString();
        var now = clock.GetUtcNow();
        var stream = new TypeIIPeriod(request.TenantId, request.ProgramId, request.PeriodId).Stream;
        var provenance = new OperationProvenance(request.CommandId, request.AmendmentId, null, CommandKind,
            ActorReference.ForMember(actorMemberId, actorDisplay), stream, InitialEpisode,
            (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now, stream, stream);
        var binding = AcceptedOperationBinding.Of(provenance);
        ImmutableContentRef? result = null;
        try
        {
            var accepted = await operations.ExecuteAsync(new NativeOperation<TypeIIPeriod>(binding,
                () => new TypeIIPeriod(request.TenantId, request.ProgramId, request.PeriodId, request.CommandId, request.AmendmentId),
                period => period.Acceptance,
                async (period, token) =>
                {
                    if (period.ProgramId != request.ProgramId)
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound, "The period was not found in this program."));
                    var guard = period.PrepareCancel(request.AmendmentId, request.ExpectedRevision);
                    if (!guard.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(guard.Error!);
                    try
                    {
                        var response = await NativeContent.FreezeUnitAsync(content, request.TenantId, token).ConfigureAwait(false);
                        result = response.Result;
                        return Result<FirstResponseDescriptor>.Success(response.Response);
                    }
                    catch (ImmutableContentIntegrityException exception)
                    {
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The exact cancellation response cannot be verified.", exception is ImmutableContentUnavailableException));
                    }
                }, (period, first) => period.CancelAmendment(request.AmendmentId, provenance, first, result!)), context, ct).ConfigureAwait(false);
            return await NativeContent.ResponseAsync<UnitResult>(content, request.TenantId, accepted, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<UnitResult>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The original period cancellation response cannot be verified.", exception is ImmutableContentUnavailableException));
        }
    }
}
