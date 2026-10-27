using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

/// <summary>Delivers an identity recovery challenge; the worker records every attempt as a typed outcome.</summary>
public sealed class DeliverRecoveryChallengeHandler(EmailChallengeDeliveryWorker worker)
    : IRequestHandler<DeliverRecoveryChallenge, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<DeliverRecoveryChallenge> context,
        CancellationToken ct) => worker.DeliverAsync(context.Request, context, ct);
}
