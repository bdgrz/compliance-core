using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Durable CAS store of the technical delivery state of email challenges, keyed by challenge.</summary>
public interface IEmailChallengeDeliveryLedger
{
    /// <summary>Reads the delivery record, or null when no attempt was ever opened.</summary>
    Task<EmailChallengeDeliveryRecord?> ReadAsync(Uuid challengeId, CancellationToken ct = default);

    /// <summary>
    ///     Opens the next attempt durably, before the relay is called. A delivered, failed or already open record is
    ///     returned unchanged, so a resumed step names the attempt it already opened.
    /// </summary>
    Task<EmailChallengeDeliveryRecord> OpenAttemptAsync(Uuid challengeId, string purpose, DateTimeOffset now,
        CancellationToken ct = default);

    /// <summary>Applies the outcome of one attempt by CAS and returns the decision and the stored record.</summary>
    Task<EmailChallengeOutcomeDecision> RecordOutcomeAsync(Uuid challengeId, Uuid attemptId, string outcome,
        string? failureCode, Uuid commandId, DateTimeOffset at, CancellationToken ct = default);
}
