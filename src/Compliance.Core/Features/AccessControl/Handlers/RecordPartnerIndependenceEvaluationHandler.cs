using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     Admits the designated partner's personal independence evaluation against the exact engagement revision, service
///     epoch and ratified rule version and acknowledges it with a fixed 202; admission alone approves nothing.
/// </summary>
sealed class RecordPartnerIndependenceEvaluationHandler(EngagementPersonalDecisionAdmission admission)
    : IRequestHandler<RecordPartnerIndependenceEvaluation, OperationAcceptedResponse>
{
    public ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<RecordPartnerIndependenceEvaluation> context, CancellationToken ct) =>
        admission.EvaluateAsync(context, ct);
}
