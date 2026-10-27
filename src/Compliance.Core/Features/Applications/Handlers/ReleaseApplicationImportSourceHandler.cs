using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Handlers;

sealed class ReleaseApplicationImportSourceHandler(ApplicationImportReceivers receivers)
    : IRequestHandler<ReleaseApplicationImportSource, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ReleaseApplicationImportSource> context,
        CancellationToken ct) => receivers.ReleaseSourceAsync(context, context.Request, ct);
}
