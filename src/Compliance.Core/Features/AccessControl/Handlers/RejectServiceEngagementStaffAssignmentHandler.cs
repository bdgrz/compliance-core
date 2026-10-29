using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

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
