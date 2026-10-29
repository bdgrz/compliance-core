using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.Applications.Handlers;

sealed class ReleaseApplicationImportTargetHandler(ApplicationImportReceivers receivers)
    : IRequestHandler<ReleaseApplicationImportTarget, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ReleaseApplicationImportTarget> context,
        CancellationToken ct) => receivers.ReleaseTargetAsync(context, context.Request, ct);
}
