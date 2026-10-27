using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Admits one positive actual staff assignment to an accepted engagement by its designated partner against the
///     exact engagement revision and acknowledges it with a fixed 202; the assignment is recorded only by the
///     ordered acceptance process.
/// </summary>
sealed class AssignServiceEngagementStaffHandler(EngagementAcceptanceAdmission admission)
    : IRequestHandler<AssignServiceEngagementStaff, OperationAcceptedResponse>
{
    public ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<AssignServiceEngagementStaff> context, CancellationToken ct) =>
        admission.AssignAsync(context, ct);
}
