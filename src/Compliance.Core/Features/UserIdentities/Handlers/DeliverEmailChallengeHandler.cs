using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Delivers a verification challenge; the worker records every attempt as a typed outcome.</summary>
public sealed class DeliverEmailChallengeHandler(EmailChallengeDeliveryWorker worker)
    : IRequestHandler<DeliverEmailChallenge, ReceiverOutcome>
{
    public ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<DeliverEmailChallenge> context,
        CancellationToken ct) => worker.DeliverAsync(context.Request, context, ct);
}
