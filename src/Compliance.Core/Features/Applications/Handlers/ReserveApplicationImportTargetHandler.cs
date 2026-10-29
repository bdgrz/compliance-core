using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.Applications.Handlers;

sealed class ReserveApplicationImportTargetHandler(ApplicationImportReceivers receivers)
    : IRequestHandler<ReserveApplicationImportTarget, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ReserveApplicationImportTarget> context,
        CancellationToken ct) => receivers.ReserveAsync(context, context.Request, ct);
}
