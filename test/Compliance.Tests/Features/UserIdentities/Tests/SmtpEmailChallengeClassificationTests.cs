using System.Net.Mail;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;

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
