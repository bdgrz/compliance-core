using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

sealed class ClaimFirmStaffIdentityHandler(FirmStaffRegistrationReceivers receivers)
    : IRequestHandler<ClaimFirmStaffIdentity, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ClaimFirmStaffIdentity> context,
        CancellationToken ct) => receivers.ClaimAsync(context, context.Request, ct);
}
