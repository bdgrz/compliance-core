using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Handlers;

sealed class AcquireTypeIIPeriodRangeHandler(TypeIIRangeReceivers receivers)
    : IRequestHandler<AcquireTypeIIPeriodRange, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<AcquireTypeIIPeriodRange> context,
        CancellationToken ct) => receivers.AcquireAsync(context, context.Request, ct);
}
