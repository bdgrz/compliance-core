using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

sealed class RejectServiceEngagementAcceptanceHandler(EngagementRejection rejection)
    : IRequestHandler<RejectServiceEngagementAcceptance, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<RejectServiceEngagementAcceptance> context,
        CancellationToken ct)
    {
        var command = context.Request;
        return rejection.RejectAsync(command.CommandId, command.OperationId, command.Target, command.Generation,
            command.FailureProof, EngagementIndependenceOperationKind.Acceptance,
            nameof(RejectServiceEngagementAcceptance), context, ct);
    }
}
