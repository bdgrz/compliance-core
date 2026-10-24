using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     The technical delivery state of one email challenge: its bounded list of attempts. It is process state, not a
///     fact of the address stream. The record never holds a token, a recipient or a provider message; it is the
///     source of truth for whether the challenge was delivered, definitively failed, or is still being delivered.
/// </summary>
public sealed record EmailChallengeDeliveryRecord(Uuid ChallengeId, string Purpose,
    IReadOnlyList<EmailChallengeDeliveryAttempt> Attempts, long Version)
{
    /// <summary>Attempts one challenge may use, including the first; the record stays bounded.</summary>
    public const int MaximumAttempts = 5;

    /// <summary>The deterministic identity of an attempt, so a resumed process names the attempt it already opened.</summary>
    public static Uuid AttemptIdOf(Uuid challengeId, int number) =>
        Uuid.CreateVersion5(challengeId, Encoding.UTF8.GetBytes($"email-challenge-delivery-attempt:{number}"));

    /// <summary>A record without attempts.</summary>
    public static EmailChallengeDeliveryRecord Empty(Uuid challengeId, string purpose) =>
        new(challengeId, purpose, [], 0);

    /// <summary>The most recent attempt.</summary>
    public EmailChallengeDeliveryAttempt? Latest => Attempts.Count == 0 ? null : Attempts[^1];

    /// <summary>True once an attempt was accepted by the relay.</summary>
    public bool IsDelivered => Attempts.Any(attempt => attempt.Outcome == EmailChallengeDeliveryAttempt.Sent);

    /// <summary>
    ///     True when no further attempt will be made: the latest attempt was a definite permanent rejection, or the
    ///     attempt budget is spent without an acceptance.
    /// </summary>
    public bool IsFailed => !IsDelivered && Latest is { IsOpen: false } latest &&
                            (latest.Outcome == EmailChallengeFailureCertainty.DefinitePermanent ||
                             Attempts.Count >= MaximumAttempts);

    /// <summary>True while the latest attempt has no outcome, as after a crash between the relay call and its outcome.</summary>
    public bool HasOpenAttempt => Latest is { IsOpen: true };

    /// <summary>The status the challenge status read reports: "pending", "delivered" or "failed".</summary>
    public string Status => IsDelivered ? "delivered" : IsFailed ? "failed" : "pending";

    /// <summary>Opens the next attempt. Callers check <see cref="IsDelivered" />, <see cref="IsFailed" /> and <see cref="HasOpenAttempt" /> first.</summary>
    public EmailChallengeDeliveryRecord WithNextAttempt(DateTimeOffset startedAt)
    {
        if (IsDelivered || IsFailed || HasOpenAttempt || Attempts.Count >= MaximumAttempts)
            throw new InvalidOperationException("No further delivery attempt can be opened.");
        var number = Attempts.Count + 1;
        return this with
        {
            Attempts = [.. Attempts, new EmailChallengeDeliveryAttempt(number, AttemptIdOf(ChallengeId, number),
                startedAt, null, null, null, null)],
        };
    }

    /// <summary>Decides what an outcome of <paramref name="attemptId" /> does to this record.</summary>
    public EmailChallengeOutcomeDecision Decide(Uuid attemptId, string outcome, string? failureCode, Uuid commandId,
        DateTimeOffset at)
    {
        var index = Attempts.ToList().FindIndex(attempt => attempt.AttemptId == attemptId);
        if (index < 0)
            return new EmailChallengeOutcomeDecision(EmailChallengeOutcomeKind.UnknownAttempt, this);
        var attempt = Attempts[index];
        if (index < Attempts.Count - 1)
            // A newer attempt exists, so this outcome is stale and must not change the record.
            return new EmailChallengeOutcomeDecision(EmailChallengeOutcomeKind.Stale, this);
        var closed = attempt with { Outcome = outcome, FailureCode = failureCode, ClosedAt = at, ClosedBy = commandId };
        if (attempt.IsOpen)
            return new EmailChallengeOutcomeDecision(EmailChallengeOutcomeKind.Closed, Replace(index, closed));
        if (attempt.Outcome == outcome && attempt.FailureCode == failureCode)
            return new EmailChallengeOutcomeDecision(EmailChallengeOutcomeKind.Replayed, this);
        // Evidence about an ambiguous latest attempt settles it; any other disagreement is a conflict.
        return attempt.Outcome == EmailChallengeFailureCertainty.Unknown && outcome != EmailChallengeFailureCertainty.Unknown
            ? new EmailChallengeOutcomeDecision(EmailChallengeOutcomeKind.Resolved, Replace(index, closed))
            : new EmailChallengeOutcomeDecision(EmailChallengeOutcomeKind.Conflict, this);
    }

    EmailChallengeDeliveryRecord Replace(int index, EmailChallengeDeliveryAttempt attempt)
    {
        var attempts = Attempts.ToArray();
        attempts[index] = attempt;
        return this with { Attempts = attempts };
    }

    /// <summary>Value equality; attempts compare by sequence so a stored record equals its round trip.</summary>
    public bool Equals(EmailChallengeDeliveryRecord? other) =>
        other is not null && ChallengeId == other.ChallengeId && Purpose == other.Purpose &&
        Version == other.Version && Attempts.SequenceEqual(other.Attempts);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(ChallengeId, Purpose, Version);
}
