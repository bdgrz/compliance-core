using Cntryl.Portia;
using Bdgrz.Compliance.Features.Operations.Processes;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.Operations.Handlers;

sealed class RejectTypeIIPeriodAmendmentHandler(TypeIIRangeReceivers receivers)
    : IRequestHandler<RejectTypeIIPeriodAmendment, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<RejectTypeIIPeriodAmendment> context,
        CancellationToken ct) => receivers.RejectAmendmentAsync(context, context.Request, ct);
}
