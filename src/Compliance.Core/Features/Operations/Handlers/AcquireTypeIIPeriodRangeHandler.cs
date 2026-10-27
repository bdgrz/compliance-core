using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.Operations.Handlers;

sealed class AcquireTypeIIPeriodRangeHandler(TypeIIRangeReceivers receivers)
    : IRequestHandler<AcquireTypeIIPeriodRange, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<AcquireTypeIIPeriodRange> context,
        CancellationToken ct) => receivers.AcquireAsync(context, context.Request, ct);
}
