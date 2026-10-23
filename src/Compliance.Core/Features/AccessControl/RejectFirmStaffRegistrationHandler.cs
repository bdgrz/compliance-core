using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class RejectFirmStaffRegistrationHandler(FirmStaffRegistrationReceivers receivers)
    : IRequestHandler<RejectFirmStaffRegistration, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<RejectFirmStaffRegistration> context,
        CancellationToken ct) => receivers.RejectAsync(context, context.Request, ct);
}
