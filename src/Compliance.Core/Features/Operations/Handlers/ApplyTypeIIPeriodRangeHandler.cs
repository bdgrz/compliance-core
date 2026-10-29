using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.Operations.Handlers;

sealed class ApplyTypeIIPeriodRangeHandler(TypeIIRangeReceivers receivers)
    : IRequestHandler<ApplyTypeIIPeriodRange, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ApplyTypeIIPeriodRange> context,
        CancellationToken ct) => receivers.ApplyAsync(context, context.Request, ct);
}
