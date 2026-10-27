using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Handlers;

sealed class ApplyApplicationImportEffectHandler(ApplicationImportReceivers receivers)
    : IRequestHandler<ApplyApplicationImportEffect, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ApplyApplicationImportEffect> context,
        CancellationToken ct) => receivers.ApplyAsync(context, context.Request, ct);
}
