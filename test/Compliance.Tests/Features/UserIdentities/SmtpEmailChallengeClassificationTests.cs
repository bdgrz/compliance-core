using System.Net.Mail;
using Bdgrz.Compliance.Features.Work;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

/// <summary>Only an explicit SMTP reply proves non-acceptance; every other result is ambiguous and reconciled before a resend.</summary>
public sealed class SmtpEmailChallengeClassificationTests
{
    [Fact]
    public void ShouldClassifyExactlyAsTheDigestTransportGivenTheSameFailures()
    {
        // Arrange
        Exception[] failures =
        [
            new SmtpException(SmtpStatusCode.MailboxBusy),
            new SmtpException(SmtpStatusCode.MailboxUnavailable),
            new SmtpException(SmtpStatusCode.GeneralFailure),
            new TimeoutException("connection lost after DATA"),
            new IOException("broken pipe"),
        ];

        // Act
        var challenge = failures.Select(failure => SmtpEmailChallengeDelivery.Classify(failure).Certainty).ToArray();
        var digest = failures.Select(failure => SmtpWorkDigestDelivery.Classify(failure).Kind).ToArray();

        // Assert
        Assert.Equal([EmailChallengeFailureCertainty.DefiniteTransient, EmailChallengeFailureCertainty.DefinitePermanent,
            EmailChallengeFailureCertainty.Unknown, EmailChallengeFailureCertainty.Unknown,
            EmailChallengeFailureCertainty.Unknown], challenge);
        Assert.Equal([WorkDigestTransportOutcomeKind.DefiniteTransientRejection,
            WorkDigestTransportOutcomeKind.DefinitePermanentRejection, WorkDigestTransportOutcomeKind.Unknown,
            WorkDigestTransportOutcomeKind.Unknown, WorkDigestTransportOutcomeKind.Unknown], digest);
    }

    [Fact]
    public void ShouldRedactTheProviderMessageGivenARelayFailure()
    {
        // Arrange
        var failure = new SmtpException(SmtpStatusCode.MailboxUnavailable, "550 person@example.com rejected by provider-secret");

        // Act
        var classified = SmtpEmailChallengeDelivery.Classify(failure);

        // Assert
        Assert.DoesNotContain("person@example.com", classified.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("provider-secret", classified.Message, StringComparison.Ordinal);
        Assert.Equal("smtp_permanent_rejection", classified.FailureCode);
    }
}
