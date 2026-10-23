using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class ApplyProfessionalDutyChangeHandler(ProfessionalDutyChangeReceivers receivers)
    : IRequestHandler<ApplyProfessionalDutyChange, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ApplyProfessionalDutyChange> context,
        CancellationToken ct) => receivers.ApplyAsync(context, context.Request, ct);
}
