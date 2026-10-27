using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

sealed class SettleTypeIIPeriodRangeHandler(TypeIIRangeReceivers receivers)
    : IRequestHandler<SettleTypeIIPeriodRange, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<SettleTypeIIPeriodRange> context,
        CancellationToken ct) => receivers.SettleAsync(context, context.Request, ct);
}
