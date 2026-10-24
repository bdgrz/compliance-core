using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class ChooseRiskTreatmentHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<ChooseRiskTreatment>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ChooseRiskTreatment> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A treatment requires its command identity."));
        var actor = RiskActor.From(context.Actor, request.TenantId);
        var current = await reader.HydrateAsync(new RiskEvaluation(request.TenantId, request.RiskId, request.CommandId), ct)
            .ConfigureAwait(false);
        if (current.ReplayTreatment(request, actor.MemberId) is { } original)
            return original;
        var risk = await reader.HydrateAsync(new RiskDraft(request.TenantId, request.RiskId), ct)
            .ConfigureAwait(false);
        if (!risk.IsCreated || risk.ProgramId != request.ProgramId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The risk was not found."));
        return await executor.ExecuteAsync(new RiskEvaluation(request.TenantId, request.RiskId, request.CommandId),
            evaluation => CommandFailureRequestAdapter.ToOutcome(evaluation.ChooseTreatment(request,
                actor.MemberId, actor.Display, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
