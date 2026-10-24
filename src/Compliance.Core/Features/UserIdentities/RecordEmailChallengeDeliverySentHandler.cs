using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Records that the relay accepted one delivery attempt. An outcome of an attempt that a newer attempt replaced is
///     ignored, an exact repeat changes nothing, and a different outcome for the same attempt conflicts.
/// </summary>
public sealed class RecordEmailChallengeDeliverySentHandler(IEmailChallengeDeliveryLedger ledger)
    : IRequestHandler<RecordEmailChallengeDeliverySent>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RecordEmailChallengeDeliverySent> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ChallengeId == Uuid.Empty || request.AttemptId == Uuid.Empty ||
            request.SentAt == default)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A delivery outcome requires its command, challenge, attempt and time."));
        var decision = await ledger.RecordOutcomeAsync(request.ChallengeId, request.AttemptId,
            EmailChallengeDeliveryAttempt.Sent, null, request.CommandId, request.SentAt, ct).ConfigureAwait(false);
        return EmailChallengeDeliveryOutcomes.ToResult(decision);
    }
}
