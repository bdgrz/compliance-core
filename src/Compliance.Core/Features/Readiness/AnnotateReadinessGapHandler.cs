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
public sealed class AnnotateReadinessGapHandler(NativeOperationExecutor operations, IAggregateReader reader,
    ReadinessReader readiness, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<AnnotateReadinessGap, ReadinessAnnotationView>
{
    public async ValueTask<Result<ReadinessAnnotationView>> HandleAsync(IRequestContext<AnnotateReadinessGap> context,
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
        var snapshot = await reader.HydrateAsync(new ReadinessGapPlan(request.TenantId, request.ProgramId,
            request.GapId, request.CommandId), ct).ConfigureAwait(false);
        var provenance = ReadinessContent.Provenance(request.CommandId, "AnnotateReadinessGap", snapshot.Stream,
            actorReference, (ulong)request.ExpectedRevision, ReadinessCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? annotationContent = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            try
            {
                var program = await readiness.ReadProgramAsync(request.TenantId, request.ProgramId, ct)
                    .ConfigureAwait(false);
                if (program.AdoptionOf(request.AssessmentId) is null ||
                    await readiness.ReadContentAsync(request.TenantId, request.ProgramId, request.AssessmentId, ct)
                        .ConfigureAwait(false) is not { } loaded || loaded.Gaps.All(gap => gap.GapId != request.GapId))
                    return Failure(RequestErrorKind.NotFound, "The readiness gap was not found.");
                var prepared = snapshot.PrepareAnnotate(request.ExpectedRevision);
                if (!prepared.IsSuccess)
                    return Result<ReadinessAnnotationView>.Failure(prepared.Error!);
                if (program.LatestAssessmentId != request.AssessmentId)
                    return Failure(RequestErrorKind.Conflict, "A later assessment exists; annotate the latest assessment.");
            }
            catch (ImmutableContentIntegrityException exception)
            {
                return Result<ReadinessAnnotationView>.Failure(GetReadinessAssessmentHandler.Unverified(exception));
            }

            if (!ReadinessDecisionRules.IsBoundedText(request.Body))
                return Failure(RequestErrorKind.Validation, "An annotation requires a body of at most 4000 characters.");
            var view = new ReadinessAnnotationView(request.CommandId, request.AssessmentId, request.GapId,
                request.Body.Trim(), actor.MemberId, actorReference, now);
            annotationContent = await ReadinessContent.WriteAsync(content, request.TenantId,
                ReadinessContent.AnnotationSchema, view, ct).ConfigureAwait(false);
            (response, result) = await ReadinessContent.FreezeAsync(content, request.TenantId,
                ReadinessContent.AnnotationViewSchema, view, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<ReadinessGapPlan>(binding,
            () => new ReadinessGapPlan(request.TenantId, request.ProgramId, request.GapId, request.CommandId),
            plan => plan.Acceptance,
            plan => ReadinessCommands.Prepared(plan.PrepareAnnotate(request.ExpectedRevision), response),
            (ReadinessGapPlan plan, ImmutableContentRef frozen) =>
                plan.Annotate(provenance, annotationContent!, frozen, result!)), context, ct).ConfigureAwait(false);
        return await ReadinessContent.ResponseAsync<ReadinessAnnotationView>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    static Result<ReadinessAnnotationView> Failure(RequestErrorKind kind, string message) =>
        Result<ReadinessAnnotationView>.Failure(new RequestError(kind, message));
}
