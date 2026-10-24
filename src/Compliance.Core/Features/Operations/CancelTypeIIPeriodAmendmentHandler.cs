using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Cancels a pending amendment. The cancellation fact is appended on the period owner, which is the local
///     receiver fence: a delayed <c>ApplyTypeIIPeriodRange</c> sees it and does nothing. Only afterwards does the
///     release reactor settle the pending calendar reservation, so the old range stays held throughout. An amendment
///     that already applied is irreversible; the cancellation conflicts and the applied range recovers forward.
/// </summary>
public sealed class CancelTypeIIPeriodAmendmentHandler(NativeOperationExecutor operations,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<CancelTypeIIPeriodAmendment>
{
    const string CommandKind = "CancelTypeIIPeriodAmendment";
    const string InitialEpisode = "initial";
    const int MaximumReasonLength = 2000;

    public async ValueTask<Result> HandleAsync(IRequestContext<CancelTypeIIPeriodAmendment> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Canceling a Type II period amendment requires a personal HTTP actor."));
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.PeriodId == Uuid.Empty || request.AmendmentId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A command id, tenant, program, period and amendment are required."));
        if (request.ExpectedRevision < 0 || string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Trim().Length > MaximumReasonLength)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A cancellation requires a non-negative expected revision and a bounded reason."));

        var realm = request.TenantId.ToString();
        var now = clock.GetUtcNow();
        var stream = new TypeIIPeriod(request.TenantId, request.ProgramId, request.PeriodId).Stream;
        var provenance = new OperationProvenance(request.CommandId, request.AmendmentId, null, CommandKind,
            ActorReference.ForMember(actorMemberId, actorDisplay), stream, InitialEpisode,
            (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now, stream, stream);
        var binding = AcceptedOperationBinding.Of(provenance);
        var unitBody = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), UnitResult.Schema,
            new UnitResult(), ct).ConfigureAwait(false);
        var response = new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType,
            UnitResult.Schema, new StandardResponseBody(unitBody));
        var accepted = await operations.ExecuteAsync(new NativeOperation<TypeIIPeriod>(binding,
            () => new TypeIIPeriod(request.TenantId, request.ProgramId, request.PeriodId, request.CommandId),
            period => period.Acceptance,
            period => Prepare(period, request, response),
            (TypeIIPeriod period, ImmutableContentRef frozen) =>
                period.CancelAmendment(request.AmendmentId, provenance, frozen)), context, ct)
            .ConfigureAwait(false);
        if (!accepted.IsSuccess)
            return Result.Failure(accepted.Error!);
        return accepted.Value switch
        {
            AcceptedAppendResult.Committed or AcceptedAppendResult.Replayed => Result.Success,
            _ => Result.Failure(AcceptedAppendResult.ConflictError),
        };
    }

    static Result<FirstResponseDescriptor> Prepare(TypeIIPeriod period, CancelTypeIIPeriodAmendment request,
        FirstResponseDescriptor response)
    {
        var prepared = period.PrepareCancel(request.AmendmentId, request.ExpectedRevision);
        return prepared.IsSuccess
            ? Result<FirstResponseDescriptor>.Success(response)
            : Result<FirstResponseDescriptor>.Failure(prepared.Error!);
    }
}
