using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Records the reviewer's own independent decision; HTTP-only. Only the plan's reviewer or a
///     program manager may review, and never the performer or recorder without a waiver.
/// </summary>
public sealed class ReviewControlOccurrenceHandler(NativeOperationExecutor operations,
    ControlOperationsReader operatingReader, IAggregateReader reader, ControlReader controlSources, OperatingAuthority authority,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<ReviewControlOccurrence, ControlOccurrenceView>
{
    public ValueTask<Result<ControlOccurrenceView>> HandleAsync(
        IRequestContext<ReviewControlOccurrence> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<ControlOccurrenceView>> HandleCoreAsync(
        IRequestContext<ReviewControlOccurrence> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result<ControlOccurrenceView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Occurrence proofs require personal HTTP submission."));
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result<ControlOccurrenceView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A review requires a command id and a non-negative expected revision."));
        var now = clock.GetUtcNow();
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        return await OccurrenceProofs.ReviewAsync(operations, content, context, request,
            AuthoredIntentDigest.Compute(request), actor, async token =>
            {
                var today = DateOnly.FromDateTime(now.UtcDateTime);
                var control = await ControlOperationsSource.LoadControlAsync(controlSources, request.TenantId, request.ProgramId,
                    request.ControlId, token).ConfigureAwait(false);
                if (control is null)
                    return Result<PreparedOccurrenceReview>.Failure(ControlOperationsSource.ControlNotFound());
                var windows = OperatingAuthority.VersionWindows(control);
                var snapshot = await operatingReader.ReadOccurrenceAsync(request.TenantId, request.ProgramId,
                    request.ControlId, request.OccurrenceId, token).ConfigureAwait(false);
                if (snapshot.ReadOccurrence(request.ControlId, request.OccurrenceId, windows, today) is not { } occurrence ||
                    snapshot.FindPlan(request.ControlId, occurrence.PlanVersionId) is not { } plan)
                    return Result<PreparedOccurrenceReview>.Failure(ControlOperationsSource.OccurrenceNotFound());
                var reviewer = (snapshot.CurrentPlan(request.ControlId) ?? plan).ReviewerMemberId;
                if (actor.MemberId != reviewer && !await authority.ManagesProgramAsync(request.TenantId, actor,
                        request.ProgramId, token).ConfigureAwait(false))
                    return Result<PreparedOccurrenceReview>.Failure(new RequestError(RequestErrorKind.Forbidden,
                        "Only the assigned reviewer or a program manager may review this occurrence."));
                SeparationOfDutiesWaiver? waiver = null;
                if (request.SeparationOfDutiesWaiverId is { } waiverId)
                    waiver = await reader.HydrateAsync(new SeparationOfDutiesWaiver(request.TenantId, waiverId), token)
                        .ConfigureAwait(false);
                return Result<PreparedOccurrenceReview>.Success(new PreparedOccurrenceReview(occurrence, waiver));
            }, now, ct).ConfigureAwait(false);
    }
}
