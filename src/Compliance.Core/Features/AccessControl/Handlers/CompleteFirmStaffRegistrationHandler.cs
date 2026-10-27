using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

sealed class CompleteFirmStaffRegistrationHandler(FirmStaffRegistrationReceivers receivers)
    : IRequestHandler<CompleteFirmStaffRegistration, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<CompleteFirmStaffRegistration> context,
        CancellationToken ct) => receivers.CompleteAsync(context, context.Request, ct);
}
