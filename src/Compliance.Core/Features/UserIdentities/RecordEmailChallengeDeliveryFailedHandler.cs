using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Records that one delivery attempt did not end in an accepted message, with how much that proves. An outcome of an
///     attempt that a newer attempt replaced is ignored, an exact repeat changes nothing, and a different outcome for
///     the same attempt conflicts unless it settles an ambiguous latest attempt.
/// </summary>
public sealed class RecordEmailChallengeDeliveryFailedHandler(IEmailChallengeDeliveryLedger ledger)
    : IRequestHandler<RecordEmailChallengeDeliveryFailed>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RecordEmailChallengeDeliveryFailed> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ChallengeId == Uuid.Empty || request.AttemptId == Uuid.Empty ||
            request.FailedAt == default || !EmailChallengeFailureCertainty.IsDefined(request.Certainty) ||
            !EmailChallengeDeliveryOutcomes.IsFailureCode(request.FailureCode))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A delivery failure requires its command, challenge, attempt, time, certainty and a redacted code."));
        var decision = await ledger.RecordOutcomeAsync(request.ChallengeId, request.AttemptId, request.Certainty,
            request.FailureCode, request.CommandId, request.FailedAt, ct).ConfigureAwait(false);
        return EmailChallengeDeliveryOutcomes.ToResult(decision);
    }
}
