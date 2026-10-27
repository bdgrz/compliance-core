using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Handlers;

sealed class RejectTypeIIPeriodOpeningHandler(TypeIIRangeReceivers receivers)
    : IRequestHandler<RejectTypeIIPeriodOpening, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<RejectTypeIIPeriodOpening> context,
        CancellationToken ct) => receivers.RejectOpeningAsync(context, context.Request, ct);
}
