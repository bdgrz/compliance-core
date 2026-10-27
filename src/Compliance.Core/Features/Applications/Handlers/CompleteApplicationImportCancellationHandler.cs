using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Handlers;

sealed class CompleteApplicationImportCancellationHandler(ApplicationImportReceivers receivers)
    : IRequestHandler<CompleteApplicationImportCancellation, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<CompleteApplicationImportCancellation> context, CancellationToken ct) =>
        receivers.CompleteAsync(context, context.Request, ct);
}
