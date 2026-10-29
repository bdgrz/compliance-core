using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.Applications.Handlers;

sealed class ClaimApplicationImportSourceHandler(ApplicationImportReceivers receivers)
    : IRequestHandler<ClaimApplicationImportSource, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ClaimApplicationImportSource> context,
        CancellationToken ct) => receivers.ClaimAsync(context, context.Request, ct);
}
