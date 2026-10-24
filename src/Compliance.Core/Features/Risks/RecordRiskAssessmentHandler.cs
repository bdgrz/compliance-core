using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class RecordRiskAssessmentHandler(IAggregateExecutor executor,
    IAggregateReader reader, RiskGovernanceReader governanceReader, TimeProvider clock)
    : IRequestHandler<RecordRiskAssessment, RiskAssessmentView>
{
    public async ValueTask<Result<RiskAssessmentView>> HandleAsync(
        IRequestContext<RecordRiskAssessment> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty)
            return Result<RiskAssessmentView>.Failure(new RequestError(RequestErrorKind.Validation,
                "An assessment requires its typed command identity."));
        var actor = RiskActor.From(context.Actor, request.TenantId);
        var snapshot = await reader.HydrateAsync(new RiskEvaluation(request.TenantId, request.RiskId, request.CommandId), ct)
            .ConfigureAwait(false);
        if (snapshot.ReplayAssessment(request, actor.MemberId) is { } original)
            return original;
        var risk = await reader.HydrateAsync(new RiskDraft(request.TenantId, request.RiskId), ct)
            .ConfigureAwait(false);
        if (!risk.IsCreated || risk.ProgramId != request.ProgramId)
            return Result<RiskAssessmentView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The risk was not found."));
        var methods = await reader.HydrateAsync(new RiskMethod(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (methods.GetVersion(request.MethodVersion) is not { } method)
            return Result<RiskAssessmentView>.Failure(new RequestError(
                RequestErrorKind.Validation, "The risk method version was not found."));
        var governance = await governanceReader.ReadRiskAsync(request.TenantId, request.ProgramId,
            request.RiskId, ct).ConfigureAwait(false);
        var hasControlTreatment = governance.HasAcceptedControlTreatment(request.RiskId);
        return await executor.ExecuteAsync(new RiskEvaluation(request.TenantId, request.RiskId, request.CommandId),
            evaluation =>
            {
                var failure = evaluation.RecordAssessment(request.ProgramId,
                    request.ExpectedRevision, request.CommandId, method, request.Phase,
                    request.Likelihood, request.Impact, request.Rationale, actor.MemberId,
                    actor.Display, clock.GetUtcNow(), hasControlTreatment);
                return CommandFailureRequestAdapter.ToOutcome(failure,
                    evaluation.FindAssessment(request.CommandId)!);
            }, context, ct).ConfigureAwait(false);
    }
}
