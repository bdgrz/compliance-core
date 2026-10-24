using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Admits the acceptance of an engagement by its designated partner against the exact engagement revision and
///     acknowledges it with a fixed 202; the engagement is accepted only by the ordered acceptance process.
/// </summary>
sealed class AcceptServiceEngagementHandler(EngagementAcceptanceAdmission admission)
    : IRequestHandler<AcceptServiceEngagement, OperationAcceptedResponse>
{
    public ValueTask<Result<OperationAcceptedResponse>> HandleAsync(IRequestContext<AcceptServiceEngagement> context,
        CancellationToken ct) => admission.AcceptAsync(context, ct);
}
