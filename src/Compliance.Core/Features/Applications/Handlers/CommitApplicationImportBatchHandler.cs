using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Handlers;

sealed class CommitApplicationImportBatchHandler(ApplicationImportReceivers receivers)
    : IRequestHandler<CommitApplicationImportBatch, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<CommitApplicationImportBatch> context,
        CancellationToken ct) => receivers.CommitAsync(context, context.Request, ct);
}
