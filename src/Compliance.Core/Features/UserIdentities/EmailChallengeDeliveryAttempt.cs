using Bdgrz.Compliance.Features.UserIdentities.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     One attempt to deliver an email challenge. An attempt is open until an outcome closes it; the attempt number
///     orders attempts, and an outcome of any attempt but the latest is stale.
/// </summary>
/// <param name="Number">The one-based ordinal of the attempt.</param>
/// <param name="AttemptId">The deterministic attempt identity, derived from the challenge and the ordinal.</param>
/// <param name="StartedAt">When the attempt was durably started, before the relay was called.</param>
/// <param name="Outcome">Null while open, otherwise one of <see cref="Sent" /> or an <see cref="EmailChallengeFailureCertainty" />.</param>
/// <param name="FailureCode">The redacted failure category of a failed attempt.</param>
/// <param name="ClosedAt">When the outcome was recorded.</param>
/// <param name="ClosedBy">The command that recorded the outcome.</param>
public sealed record EmailChallengeDeliveryAttempt(int Number, Uuid AttemptId, DateTimeOffset StartedAt,
    string? Outcome, string? FailureCode, DateTimeOffset? ClosedAt, Uuid? ClosedBy)
{
    /// <summary>The outcome of an attempt the relay accepted.</summary>
    public const string Sent = "sent";

    /// <summary>True while no outcome has been recorded.</summary>
    public bool IsOpen => Outcome is null;
}
