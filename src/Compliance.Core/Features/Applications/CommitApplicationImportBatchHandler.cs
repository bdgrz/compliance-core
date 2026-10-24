using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed class CommitApplicationImportBatchHandler(ApplicationImportReceivers receivers)
    : IRequestHandler<CommitApplicationImportBatch, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<CommitApplicationImportBatch> context,
        CancellationToken ct) => receivers.CommitAsync(context, context.Request, ct);
}
