using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     A challenge message was not accepted by the relay. <see cref="Certainty" /> says whether that is known: only an
///     explicit relay rejection proves non-acceptance, so any other exception a delivery throws is treated as
///     <see cref="EmailChallengeFailureCertainty.Unknown" />. The message is generic: relay replies can echo recipients.
/// </summary>
public sealed class EmailChallengeDeliveryException(string certainty, string failureCode)
    : Exception("Email challenge delivery failed.")
{
    /// <summary>One of <see cref="EmailChallengeFailureCertainty" />.</summary>
    public string Certainty { get; } = EmailChallengeFailureCertainty.IsDefined(certainty)
        ? certainty
        : throw new ArgumentOutOfRangeException(nameof(certainty));

    /// <summary>The redacted failure category.</summary>
    public string FailureCode { get; } = failureCode;
}
