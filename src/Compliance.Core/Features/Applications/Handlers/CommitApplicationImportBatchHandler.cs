using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.Applications.Handlers;

sealed class CommitApplicationImportBatchHandler(ApplicationImportReceivers receivers)
    : IRequestHandler<CommitApplicationImportBatch, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<CommitApplicationImportBatch> context,
        CancellationToken ct) => receivers.CommitAsync(context, context.Request, ct);
}
