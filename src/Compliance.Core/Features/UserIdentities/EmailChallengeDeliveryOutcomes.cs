using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>What the two typed delivery outcome commands share: the failure code shape and the decision mapping.</summary>
static class EmailChallengeDeliveryOutcomes
{
    /// <summary>A failure code is a short lowercase category, never a provider message.</summary>
    public static bool IsFailureCode(string? code) => code is { Length: > 0 and <= 64 } &&
                                                      code.All(character => char.IsAsciiLetterLower(character) ||
                                                                            char.IsAsciiDigit(character) || character == '_');

    /// <summary>A recorded, replayed, settled or stale outcome succeeds; a contradiction or an unknown attempt conflicts.</summary>
    public static Result ToResult(EmailChallengeOutcomeDecision decision) => decision.Kind switch
    {
        EmailChallengeOutcomeKind.Closed or EmailChallengeOutcomeKind.Replayed or EmailChallengeOutcomeKind.Resolved
            or EmailChallengeOutcomeKind.Stale => Result.Success,
        EmailChallengeOutcomeKind.UnknownAttempt => Result.Failure(new RequestError(RequestErrorKind.Conflict,
            "The delivery outcome names an attempt this challenge never opened.", false)),
        _ => Result.Failure(new RequestError(RequestErrorKind.Conflict,
            "The attempt already holds a different delivery outcome.", false)),
    };
}
