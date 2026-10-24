using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed class ReserveApplicationImportTargetHandler(ApplicationImportReceivers receivers)
    : IRequestHandler<ReserveApplicationImportTarget, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ReserveApplicationImportTarget> context,
        CancellationToken ct) => receivers.ReserveAsync(context, context.Request, ct);
}
