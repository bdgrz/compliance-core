using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Processes;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Readiness.Commands;
namespace Bdgrz.Compliance.Features.Readiness.Handlers;

/// <summary>Records the pending Type I entry decision through <see cref="ReadinessDecisionCompleter" />.</summary>
sealed class CompleteTypeIEntryDecisionHandler(ReadinessDecisionCompleter completer)
    : IRequestHandler<CompleteTypeIEntryDecision, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<CompleteTypeIEntryDecision> context,
        CancellationToken ct)
    {
        var command = context.Request;
        return completer.CompleteAsync(ReadinessDecisionKind.TypeIEntry, nameof(CompleteTypeIEntryDecision),
            command.CommandId, command.Source, command.Target, command.OperationId, command.Generation, command.Intent,
            command.ProgramId, command.AssessmentId, command.PlanProofs, context, ct);
    }
}
