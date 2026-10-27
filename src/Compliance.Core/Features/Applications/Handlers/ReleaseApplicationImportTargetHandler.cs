using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Handlers;

sealed class ReleaseApplicationImportTargetHandler(ApplicationImportReceivers receivers)
    : IRequestHandler<ReleaseApplicationImportTarget, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ReleaseApplicationImportTarget> context,
        CancellationToken ct) => receivers.ReleaseTargetAsync(context, context.Request, ct);
}
