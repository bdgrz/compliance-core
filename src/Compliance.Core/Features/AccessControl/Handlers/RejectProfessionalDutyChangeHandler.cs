using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class RejectProfessionalDutyChangeHandler(ProfessionalDutyChangeReceivers receivers)
    : IRequestHandler<RejectProfessionalDutyChange, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<RejectProfessionalDutyChange> context,
        CancellationToken ct) => receivers.RejectAsync(context, context.Request, ct);
}
