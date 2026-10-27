using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.Applications.Handlers;

sealed class FenceApplicationImportTargetHandler(ApplicationImportReceivers receivers)
    : IRequestHandler<FenceApplicationImportTarget, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<FenceApplicationImportTarget> context,
        CancellationToken ct) => receivers.FenceAsync(context, context.Request, ct);
}
