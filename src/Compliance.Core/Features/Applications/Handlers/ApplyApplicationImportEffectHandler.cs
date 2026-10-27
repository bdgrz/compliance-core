using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.Applications.Handlers;

sealed class ApplyApplicationImportEffectHandler(ApplicationImportReceivers receivers)
    : IRequestHandler<ApplyApplicationImportEffect, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ApplyApplicationImportEffect> context,
        CancellationToken ct) => receivers.ApplyAsync(context, context.Request, ct);
}
