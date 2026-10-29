using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Queries;
namespace Bdgrz.Compliance.Features.Controls.Handlers;

public sealed class GetControlDecisionHandler(ControlActivationSource source)
    : IRequestHandler<GetControlDecision, ControlDecisionView>
{
    public async ValueTask<Result<ControlDecisionView>> HandleAsync(
        IRequestContext<GetControlDecision> context, CancellationToken ct)
    {
        var request = context.Request;
        var control = await source.LoadAsync(request.TenantId, request.ProgramId,
            request.ControlId, ct).ConfigureAwait(false);
        if (!control.IsSuccess)
            return Result<ControlDecisionView>.Failure(control.Error);
        var decision = control.Value.ReadDecisions()
            .FirstOrDefault(decision => decision.DecisionId == request.DecisionId);
        return decision is not null
            ? Result<ControlDecisionView>.Success(decision)
            : Result<ControlDecisionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The control decision was not found."));
    }
}
