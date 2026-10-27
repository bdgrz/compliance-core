using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.Operations.Handlers;

sealed class SettleTypeIIPeriodRangeHandler(TypeIIRangeReceivers receivers)
    : IRequestHandler<SettleTypeIIPeriodRange, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<SettleTypeIIPeriodRange> context,
        CancellationToken ct) => receivers.SettleAsync(context, context.Request, ct);
}
