using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed class FenceApplicationImportTargetHandler(ApplicationImportReceivers receivers)
    : IRequestHandler<FenceApplicationImportTarget, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<FenceApplicationImportTarget> context,
        CancellationToken ct) => receivers.FenceAsync(context, context.Request, ct);
}
