using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Reads a risk's governance composed from its own owner streams, the authoritative source. Treatment action
///     overdue state is evaluated at read time.
/// </summary>
public sealed class GetRiskGovernanceHandler(IAggregateReader reader, RiskGovernanceReader governance,
    TimeProvider clock, RiskEvaluationReader evaluations) : IRequestHandler<GetRiskGovernance, RiskGovernanceView>
{
    public async ValueTask<Result<RiskGovernanceView>> HandleAsync(
        IRequestContext<GetRiskGovernance> context, CancellationToken ct)
    {
        try
        {
            var request = context.Request;
            var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId,
                request.ProgramId, request.RiskId, ct).ConfigureAwait(false);
            if (!risk.IsSuccess)
                return Result<RiskGovernanceView>.Failure(risk.Error);
            RiskGovernanceSnapshot snapshot;
            try
            {
                snapshot = await governance.ReadRiskAsync(request.TenantId, request.ProgramId, request.RiskId, ct)
                    .ConfigureAwait(false);
            }
            catch (ImmutableContentIntegrityException exception)
            {
                return Result<RiskGovernanceView>.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The risk governance content cannot be verified yet: {exception.Message}", true));
            }

            var evaluation = await evaluations.ReadAsync(request.TenantId, request.ProgramId, request.RiskId, ct).ConfigureAwait(false);
            return Result<RiskGovernanceView>.Success(snapshot.View(request.RiskId,
                evaluation.ToView().Assessments, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)));
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<RiskGovernanceView>.Failure(RiskEvaluationReadFailure.Of(failure));
        }
    }
}
