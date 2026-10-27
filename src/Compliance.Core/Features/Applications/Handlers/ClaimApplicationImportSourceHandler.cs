using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Handlers;

sealed class ClaimApplicationImportSourceHandler(ApplicationImportReceivers receivers)
    : IRequestHandler<ClaimApplicationImportSource, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ClaimApplicationImportSource> context,
        CancellationToken ct) => receivers.ClaimAsync(context, context.Request, ct);
}
