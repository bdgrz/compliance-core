namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>What an outcome of one delivery attempt did to the delivery record.</summary>
public enum EmailChallengeOutcomeKind
{
    /// <summary>The open latest attempt was closed by the outcome.</summary>
    Closed,

    /// <summary>The same outcome was already recorded for the latest attempt; nothing changed.</summary>
    Replayed,

    /// <summary>Evidence settled an ambiguous latest attempt.</summary>
    Resolved,

    /// <summary>A newer attempt exists; the outcome is ignored and nothing changed.</summary>
    Stale,

    /// <summary>The latest attempt already holds a different outcome; nothing changed.</summary>
    Conflict,

    /// <summary>No attempt of the challenge has this identity; nothing changed.</summary>
    UnknownAttempt,
}
