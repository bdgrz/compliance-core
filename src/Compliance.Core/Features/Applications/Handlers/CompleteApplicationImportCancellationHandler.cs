using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.Applications.Handlers;

sealed class CompleteApplicationImportCancellationHandler(ApplicationImportReceivers receivers)
    : IRequestHandler<CompleteApplicationImportCancellation, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<CompleteApplicationImportCancellation> context, CancellationToken ct) =>
        receivers.CompleteAsync(context, context.Request, ct);
}
