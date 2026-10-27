using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Handlers;

sealed class ReserveApplicationImportTargetHandler(ApplicationImportReceivers receivers)
    : IRequestHandler<ReserveApplicationImportTarget, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ReserveApplicationImportTarget> context,
        CancellationToken ct) => receivers.ReserveAsync(context, context.Request, ct);
}
