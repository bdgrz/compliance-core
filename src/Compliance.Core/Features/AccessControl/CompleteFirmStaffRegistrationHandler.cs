using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class CompleteFirmStaffRegistrationHandler(FirmStaffRegistrationReceivers receivers)
    : IRequestHandler<CompleteFirmStaffRegistration, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<CompleteFirmStaffRegistration> context,
        CancellationToken ct) => receivers.CompleteAsync(context, context.Request, ct);
}
