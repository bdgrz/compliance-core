using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

sealed class ClaimProfessionalAssertionIdentityHandler(ProfessionalDutyChangeReceivers receivers)
    : IRequestHandler<ClaimProfessionalAssertionIdentity, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<ClaimProfessionalAssertionIdentity> context, CancellationToken ct) =>
        receivers.ClaimAsync(context, context.Request, ct);
}
