using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Handlers;

sealed class ApplyTypeIIPeriodRangeHandler(TypeIIRangeReceivers receivers)
    : IRequestHandler<ApplyTypeIIPeriodRange, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ApplyTypeIIPeriodRange> context,
        CancellationToken ct) => receivers.ApplyAsync(context, context.Request, ct);
}
