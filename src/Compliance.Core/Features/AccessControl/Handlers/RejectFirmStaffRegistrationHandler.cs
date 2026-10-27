using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

sealed class RejectFirmStaffRegistrationHandler(FirmStaffRegistrationReceivers receivers)
    : IRequestHandler<RejectFirmStaffRegistration, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<RejectFirmStaffRegistration> context,
        CancellationToken ct) => receivers.RejectAsync(context, context.Request, ct);
}
