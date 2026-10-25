using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Records the acting member's attributed feedback on a gap of the latest assessment on the gap's own owner; HTTP-only.
///     The frozen first response is the annotation view, and an annotation never advances the gap plan revision.
/// </summary>
public sealed class AnnotateReadinessGapHandler(NativeOperationExecutor operations,
    ReadinessReader readiness, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<AnnotateReadinessGap, ReadinessAnnotationView>
{
    public ValueTask<Result<ReadinessAnnotationView>> HandleAsync(IRequestContext<AnnotateReadinessGap> context,
        CancellationToken ct) =>
        ReadinessContent.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<ReadinessAnnotationView>> HandleCoreAsync(IRequestContext<AnnotateReadinessGap> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Failure(RequestErrorKind.Forbidden, "Readiness feedback requires personal HTTP submission.");
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.GapId == Uuid.Empty || request.AssessmentId == Uuid.Empty ||
            request.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "An annotation requires a command id, an assessment, a gap and a non-negative expected revision.");
        var actor = ReadinessActor.From(context, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var stream = new ReadinessGapPlan(request.TenantId, request.ProgramId, request.GapId).Stream;
        var provenance = ReadinessContent.Provenance(request.CommandId, "AnnotateReadinessGap", stream,
            actorReference, (ulong)request.ExpectedRevision, ReadinessCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? annotationContent = null;
        ImmutableContentRef? result = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<ReadinessGapPlan>(binding,
            () => new ReadinessGapPlan(request.TenantId, request.ProgramId, request.GapId, request.CommandId),
            plan => plan.Acceptance,
            PrepareAsync,
            (ReadinessGapPlan plan, ImmutableContentRef frozen) =>
                plan.Annotate(provenance, annotationContent!, frozen, result!)), context, ct).ConfigureAwait(false);
        return await ReadinessContent.ResponseAsync<ReadinessAnnotationView>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);

        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(ReadinessGapPlan plan, CancellationToken token)
        {
            try
            {
                var program = await readiness.ReadProgramAsync(request.TenantId, request.ProgramId, token)
                    .ConfigureAwait(false);
                if (!await readiness.HasAssessmentAsync(program, request.AssessmentId, token).ConfigureAwait(false) ||
                    await readiness.ReadContentAsync(request.TenantId, request.ProgramId, request.AssessmentId, token)
                        .ConfigureAwait(false) is not { } loaded || loaded.Gaps.All(gap => gap.GapId != request.GapId))
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound, "The readiness gap was not found."));
                var prepared = plan.PrepareAnnotate(request.ExpectedRevision);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                if (program.LatestAssessmentId != request.AssessmentId)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict, "A later assessment exists; annotate the latest assessment."));
            }
            catch (ImmutableContentIntegrityException exception)
            {
                return Result<FirstResponseDescriptor>.Failure(GetReadinessAssessmentHandler.Unverified(exception));
            }

            if (!ReadinessDecisionRules.IsBoundedText(request.Body))
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation, "An annotation requires a body of at most 4000 characters."));
            var view = new ReadinessAnnotationView(request.CommandId, request.AssessmentId, request.GapId,
                request.Body.Trim(), actor.MemberId, actorReference, now);
            annotationContent = await ReadinessContent.WriteAsync(content, request.TenantId,
                ReadinessContent.AnnotationSchema, view, token).ConfigureAwait(false);
            var frozen = await ReadinessContent.FreezeAsync(content, request.TenantId,
                ReadinessContent.AnnotationViewSchema, view, token).ConfigureAwait(false);
            result = frozen.Result;
            return Result<FirstResponseDescriptor>.Success(frozen.Response);
        }
    }

    static Result<ReadinessAnnotationView> Failure(RequestErrorKind kind, string message) =>
        Result<ReadinessAnnotationView>.Failure(new RequestError(kind, message));
}
