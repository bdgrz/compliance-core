using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Handlers;

sealed class RejectTypeIIPeriodAmendmentHandler(TypeIIRangeReceivers receivers)
    : IRequestHandler<RejectTypeIIPeriodAmendment, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<RejectTypeIIPeriodAmendment> context,
        CancellationToken ct) => receivers.RejectAmendmentAsync(context, context.Request, ct);
}
