using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class RejectServiceEngagementStaffAssignmentHandler(EngagementRejection rejection)
    : IRequestHandler<RejectServiceEngagementStaffAssignment, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<RejectServiceEngagementStaffAssignment> context, CancellationToken ct)
    {
        var command = context.Request;
        return rejection.RejectAsync(command.CommandId, command.OperationId, command.Target, command.Generation,
            command.FailureProof, EngagementIndependenceOperationKind.StaffAssignment,
            nameof(RejectServiceEngagementStaffAssignment), context, ct);
    }
}
