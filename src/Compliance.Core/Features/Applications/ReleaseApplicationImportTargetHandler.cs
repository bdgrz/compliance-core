using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed class ReleaseApplicationImportTargetHandler(ApplicationImportReceivers receivers)
    : IRequestHandler<ReleaseApplicationImportTarget, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ReleaseApplicationImportTarget> context,
        CancellationToken ct) => receivers.ReleaseTargetAsync(context, context.Request, ct);
}
