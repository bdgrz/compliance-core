using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

sealed class ApplyProfessionalDutyChangeHandler(ProfessionalDutyChangeReceivers receivers)
    : IRequestHandler<ApplyProfessionalDutyChange, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ApplyProfessionalDutyChange> context,
        CancellationToken ct) => receivers.ApplyAsync(context, context.Request, ct);
}
