using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Handlers;

sealed class SettleTypeIIPeriodRangeHandler(TypeIIRangeReceivers receivers)
    : IRequestHandler<SettleTypeIIPeriodRange, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<SettleTypeIIPeriodRange> context,
        CancellationToken ct) => receivers.SettleAsync(context, context.Request, ct);
}
