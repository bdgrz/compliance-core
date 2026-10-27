using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Handlers;

/// <summary>
///     Records the plan of one gap on its own owner. <c>ExpectedRevision</c> is the revision of the gap plan. The frozen
///     first response is the plan view, so an exact retry returns the original plan after later plans; a gap plan held
///     by a readiness decision refuses the change until the decision settles.
/// </summary>
public sealed class PlanReadinessGapHandler(NativeOperationExecutor operations,
    ReadinessReader readiness, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<PlanReadinessGap, ReadinessGapPlanView>
{
    public ValueTask<Result<ReadinessGapPlanView>> HandleAsync(IRequestContext<PlanReadinessGap> context,
        CancellationToken ct) =>
        ReadinessContent.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<ReadinessGapPlanView>> HandleCoreAsync(IRequestContext<PlanReadinessGap> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.GapId == Uuid.Empty || request.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A gap plan requires a command id, a gap and a non-negative expected revision.");
        var actor = ReadinessActor.From(context, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var stream = new ReadinessGapPlan(request.TenantId, request.ProgramId, request.GapId).Stream;
        var provenance = ReadinessContent.Provenance(request.CommandId, "PlanReadinessGap", stream,
            actorReference, (ulong)request.ExpectedRevision, ReadinessCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? planContent = null;
        ImmutableContentRef? result = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<ReadinessGapPlan>(binding,
            () => new ReadinessGapPlan(request.TenantId, request.ProgramId, request.GapId, request.CommandId),
            plan => plan.Acceptance,
            PrepareAsync,
            (ReadinessGapPlan plan, ImmutableContentRef frozen) => plan.Plan(provenance, planContent!, frozen, result!)),
            context, ct).ConfigureAwait(false);
        return await ReadinessContent.ResponseAsync<ReadinessGapPlanView>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);

        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(ReadinessGapPlan plan, CancellationToken token)
        {
            try
            {
                var program = await readiness.ReadProgramAsync(request.TenantId, request.ProgramId, token)
                    .ConfigureAwait(false);
                if (!await readiness.HasGapAsync(request.TenantId, program, request.GapId, token).ConfigureAwait(false))
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound, "The readiness gap was not found."));
            }
            catch (ImmutableContentIntegrityException exception)
            {
                return Result<FirstResponseDescriptor>.Failure(GetReadinessAssessmentHandler.Unverified(exception));
            }

            var prepared = plan.PreparePlan(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            if (request.OwnerMemberId == Uuid.Empty || request.TargetDate == default ||
                !ReadinessDecisionRules.IsBoundedText(request.Action))
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation,
                    "A gap plan requires an owner, a target date, and an action of at most 4000 characters."));
            if (request.TargetDate < DateOnly.FromDateTime(now.UtcDateTime))
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation, "A gap plan target date cannot be in the past."));
            var view = new ReadinessGapPlanView(request.GapId, request.OwnerMemberId, request.TargetDate,
                request.Action.Trim(), actorReference, now)
            { Revision = plan.Revision + 1 };
            planContent = await ReadinessContent.WriteAsync(content, request.TenantId, ReadinessContent.PlanSchema, view,
                token).ConfigureAwait(false);
            var frozen = await ReadinessContent.FreezeAsync(content, request.TenantId,
                ReadinessContent.PlanViewSchema, view, token).ConfigureAwait(false);
            result = frozen.Result;
            return Result<FirstResponseDescriptor>.Success(frozen.Response);
        }
    }

    static Result<ReadinessGapPlanView> Failure(RequestErrorKind kind, string message) =>
        Result<ReadinessGapPlanView>.Failure(new RequestError(kind, message));
}
