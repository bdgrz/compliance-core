using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
namespace Bdgrz.Compliance.Features.UserIdentities.Models;

/// <summary>How much a failed delivery attempt proves about whether the relay accepted the message.</summary>
public static class EmailChallengeFailureCertainty
{
    /// <summary>An SMTP 4xx reply proved non-acceptance; another attempt may be tried after a delay.</summary>
    public const string DefiniteTransient = "definite_transient";

    /// <summary>An SMTP 5xx reply proved non-acceptance and will not change; no further attempt is made.</summary>
    public const string DefinitePermanent = "definite_permanent";

    /// <summary>The result is ambiguous: the relay may have accepted the message. Reconciled before any retry.</summary>
    public const string Unknown = "unknown";

    /// <summary>True for a certainty this contract defines.</summary>
    public static bool IsDefined(string? certainty) =>
        certainty is DefiniteTransient or DefinitePermanent or Unknown;
}
