using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed class CompleteApplicationImportCancellationHandler(ApplicationImportReceivers receivers)
    : IRequestHandler<CompleteApplicationImportCancellation, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<CompleteApplicationImportCancellation> context, CancellationToken ct) =>
        receivers.CompleteAsync(context, context.Request, ct);
}
