using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Records the plan of one gap on its own owner. <c>ExpectedRevision</c> is the revision of the gap plan. The frozen
///     first response is the plan view, so an exact retry returns the original plan after later plans; a gap plan held
///     by a readiness decision refuses the change until the decision settles.
/// </summary>
public sealed class PlanReadinessGapHandler(NativeOperationExecutor operations, IAggregateReader reader,
    ReadinessReader readiness, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<PlanReadinessGap, ReadinessGapPlanView>
{
    public async ValueTask<Result<ReadinessGapPlanView>> HandleAsync(IRequestContext<PlanReadinessGap> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.GapId == Uuid.Empty || request.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A gap plan requires a command id, a gap and a non-negative expected revision.");
        var actor = ReadinessActor.From(context, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new ReadinessGapPlan(request.TenantId, request.ProgramId,
            request.GapId, request.CommandId), ct).ConfigureAwait(false);
        var provenance = ReadinessContent.Provenance(request.CommandId, "PlanReadinessGap", snapshot.Stream,
            actorReference, (ulong)request.ExpectedRevision, ReadinessCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? planContent = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            try
            {
                var program = await readiness.ReadProgramAsync(request.TenantId, request.ProgramId, ct)
                    .ConfigureAwait(false);
                if (!await readiness.HasGapAsync(request.TenantId, program, request.GapId, ct).ConfigureAwait(false))
                    return Failure(RequestErrorKind.NotFound, "The readiness gap was not found.");
            }
            catch (ImmutableContentIntegrityException exception)
            {
                return Result<ReadinessGapPlanView>.Failure(GetReadinessAssessmentHandler.Unverified(exception));
            }

            var prepared = snapshot.PreparePlan(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Result<ReadinessGapPlanView>.Failure(prepared.Error!);
            if (request.OwnerMemberId == Uuid.Empty || request.TargetDate == default ||
                !ReadinessDecisionRules.IsBoundedText(request.Action))
                return Failure(RequestErrorKind.Validation,
                    "A gap plan requires an owner, a target date, and an action of at most 4000 characters.");
            if (request.TargetDate < DateOnly.FromDateTime(now.UtcDateTime))
                return Failure(RequestErrorKind.Validation, "A gap plan target date cannot be in the past.");
            var view = new ReadinessGapPlanView(request.GapId, request.OwnerMemberId, request.TargetDate,
                request.Action.Trim(), actorReference, now)
            { Revision = snapshot.Revision + 1 };
            planContent = await ReadinessContent.WriteAsync(content, request.TenantId, ReadinessContent.PlanSchema, view,
                ct).ConfigureAwait(false);
            (response, result) = await ReadinessContent.FreezeAsync(content, request.TenantId,
                ReadinessContent.PlanViewSchema, view, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<ReadinessGapPlan>(binding,
            () => new ReadinessGapPlan(request.TenantId, request.ProgramId, request.GapId, request.CommandId),
            plan => plan.Acceptance,
            plan => ReadinessCommands.Prepared(plan.PreparePlan(request.ExpectedRevision), response),
            (ReadinessGapPlan plan, ImmutableContentRef frozen) => plan.Plan(provenance, planContent!, frozen, result!)),
            context, ct).ConfigureAwait(false);
        return await ReadinessContent.ResponseAsync<ReadinessGapPlanView>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    static Result<ReadinessGapPlanView> Failure(RequestErrorKind kind, string message) =>
        Result<ReadinessGapPlanView>.Failure(new RequestError(kind, message));
}
