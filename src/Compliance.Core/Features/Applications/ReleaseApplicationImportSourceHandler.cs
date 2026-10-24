using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed class ReleaseApplicationImportSourceHandler(ApplicationImportReceivers receivers)
    : IRequestHandler<ReleaseApplicationImportSource, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ReleaseApplicationImportSource> context,
        CancellationToken ct) => receivers.ReleaseSourceAsync(context, context.Request, ct);
}
