using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Reads the projected evaluation and evaluates status at read time, including open
///     reassessment triggers from the risk's governance owners.
/// </summary>
public sealed class GetRiskEvaluationHandler(RiskEvaluationReadConsistency consistency,
    RiskGovernanceReader governanceReader, TimeProvider clock)
    : IRequestHandler<GetRiskEvaluation, RiskEvaluationView>
{
    public async ValueTask<Result<RiskEvaluationView>> HandleAsync(
        IRequestContext<GetRiskEvaluation> context, CancellationToken ct)
    {
        var request = context.Request;
        var view = await consistency.GetAsync(request.TenantId, request.ProgramId,
            request.RiskId, request.MinimumRevision, ct).ConfigureAwait(false);
        if (!view.IsSuccess)
            return view;
        var governance = await governanceReader.ReadRiskAsync(request.TenantId, request.ProgramId,
            request.RiskId, ct).ConfigureAwait(false);
        var triggers = governance.OpenTriggers(request.RiskId, view.Value.Assessments);
        return Result<RiskEvaluationView>.Success(RiskEvaluationStatus.AsOf(view.Value with
        {
            OpenReassessmentTriggers = triggers,
        }, clock.GetUtcNow()));
    }
}
