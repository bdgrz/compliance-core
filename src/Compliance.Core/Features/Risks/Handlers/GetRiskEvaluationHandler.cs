using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Queries;
using Bdgrz.Compliance.Features.Risks.Readers;
namespace Bdgrz.Compliance.Features.Risks.Handlers;

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
        try
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
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<RiskEvaluationView>.Failure(RiskEvaluationReadFailure.Of(failure));
        }
    }
}
