using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

sealed class ClaimProfessionalAssertionIdentityHandler(ProfessionalDutyChangeReceivers receivers)
    : IRequestHandler<ClaimProfessionalAssertionIdentity, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<ClaimProfessionalAssertionIdentity> context, CancellationToken ct) =>
        receivers.ClaimAsync(context, context.Request, ct);
}
