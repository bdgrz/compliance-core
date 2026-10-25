using Bdgrz.Compliance.Features.SystemDescription;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using TypeIIPlanAggregate = Bdgrz.Compliance.Features.Examinations.TypeIIPlan;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records a source-bound proposal for one Type II operating plan.</summary>
public sealed class ProposeTypeIIOperatingPlanHandler(IAggregateExecutor executor,
    IAggregateReader reader, ControlReader controlSources, FindingReader findings, ControlOperationsReader operating, TimeProvider clock, SystemDescriptionReader descriptions, ExaminationEngagementReader examinations)
    : IRequestHandler<ProposeTypeIIOperatingPlan>
{
    public ValueTask<Result> HandleAsync(
        IRequestContext<ProposeTypeIIOperatingPlan> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result> HandleCoreAsync(
        IRequestContext<ProposeTypeIIOperatingPlan> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ExaminationCommandActor.TryGet(context.Actor, context.Invocation, request.TenantId,
                out var actorMemberId, out var actorDisplay))
            return Failure(RequestErrorKind.Forbidden,
                "Proposing a Type II operating plan requires a personal HTTP actor.");
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.PlanId == Uuid.Empty || request.ExaminationId == Uuid.Empty ||
            request.Content is null)
            return Failure(RequestErrorKind.Validation,
                "A Type II plan proposal requires tenant, program, plan, and examination identities with complete content.");

        var now = clock.GetUtcNow();
        var existing = await reader.HydrateAsync(new TypeIIPlanAggregate(request.TenantId,
            request.ProgramId, request.PlanId), ct).ConfigureAwait(false);
        if (existing.Current is not null)
            return await ExecuteProposalAsync().ConfigureAwait(false);

        var sources = await TypeIIPlanSourceValidator.ValidateAsync(reader, examinations, controlSources, findings, operating, descriptions, request.TenantId,
            request.ProgramId, request.ExaminationId, request.Content, now, ct)
            .ConfigureAwait(false);
        if (!sources.IsSuccess)
            return Result.Failure(sources.Error!);

        return await ExecuteProposalAsync().ConfigureAwait(false);

        ValueTask<Result> ExecuteProposalAsync() => executor.ExecuteAsync(
            new TypeIIPlanAggregate(request.TenantId, request.ProgramId, request.PlanId), aggregate =>
                CommandFailureRequestAdapter.ToOutcome(aggregate.Propose(request.Content,
                    actorMemberId, actorDisplay, now)), context, ct);
    }

    static Result Failure(RequestErrorKind kind, string message) =>
        Result.Failure(new RequestError(kind, message));
}
