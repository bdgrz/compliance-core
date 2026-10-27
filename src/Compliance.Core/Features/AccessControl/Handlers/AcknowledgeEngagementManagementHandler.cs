using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     Admits the owning client's personal management decision against the exact engagement revision and service
///     epoch and acknowledges it with a fixed 202; never a professional approval.
/// </summary>
sealed class AcknowledgeEngagementManagementHandler(EngagementPersonalDecisionAdmission admission)
    : IRequestHandler<AcknowledgeEngagementManagement, OperationAcceptedResponse>
{
    public ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<AcknowledgeEngagementManagement> context, CancellationToken ct) =>
        admission.AcknowledgeAsync(context, ct);
}
