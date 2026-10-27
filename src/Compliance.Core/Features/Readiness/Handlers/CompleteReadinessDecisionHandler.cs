using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Handlers;

/// <summary>Records the pending readiness decision through <see cref="ReadinessDecisionCompleter" />.</summary>
sealed class CompleteReadinessDecisionHandler(ReadinessDecisionCompleter completer)
    : IRequestHandler<CompleteReadinessDecision, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<CompleteReadinessDecision> context,
        CancellationToken ct)
    {
        var command = context.Request;
        return completer.CompleteAsync(ReadinessDecisionKind.Readiness, nameof(CompleteReadinessDecision),
            command.CommandId, command.Source, command.Target, command.OperationId, command.Generation, command.Intent,
            command.ProgramId, command.AssessmentId, command.PlanProofs, context, ct);
    }
}
