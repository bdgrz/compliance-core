using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.Operations.Handlers;

sealed class RejectTypeIIPeriodOpeningHandler(TypeIIRangeReceivers receivers)
    : IRequestHandler<RejectTypeIIPeriodOpening, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<RejectTypeIIPeriodOpening> context,
        CancellationToken ct) => receivers.RejectOpeningAsync(context, context.Request, ct);
}
