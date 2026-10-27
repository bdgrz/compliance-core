using System.Net;
using System.Net.Mail;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Services;

sealed class SmtpWorkDigestDelivery(EmailChallengeDeliverySettings settings)
    : IWorkDigestDelivery
{
    public async ValueTask<WorkDigestTransportOutcome> SendAsync(
        WorkDigestDeliveryMessage digest, CancellationToken ct)
    {
        try
        {
            using var message = new MailMessage(settings.FromAddress!, digest.Recipient)
            {
                Subject = digest.Subject,
                Body = digest.TextBody,
                IsBodyHtml = false,
            };
            var domain = new MailAddress(settings.FromAddress!).Host;
            message.Headers.Add("Message-ID",
                $"<bdgrz-work-digest-{digest.MessageId.ToString().Replace("-", "", StringComparison.Ordinal)}@{domain}>");
            using var client = new SmtpClient(settings.SmtpHost!, settings.SmtpPort)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = settings.Username is null ? null :
                    new NetworkCredential(settings.Username, settings.Password),
            };
            await client.SendMailAsync(message, ct).ConfigureAwait(false);
            return new WorkDigestTransportOutcome(WorkDigestTransportOutcomeKind.Accepted);
        }
        catch (Exception exception)
        {
            // Only an explicit SMTP 4xx or 5xx reply proves non-acceptance. Every other
            // transport failure may have happened after the relay accepted DATA.
            return Classify(exception);
        }
    }

    internal static WorkDigestTransportOutcome Classify(Exception exception)
    {
        if (exception is not SmtpException smtp)
            return new WorkDigestTransportOutcome(WorkDigestTransportOutcomeKind.Unknown,
                "transport_ambiguous");
        var statusCode = (int)smtp.StatusCode;
        if (statusCode is >= 400 and < 500)
            return new WorkDigestTransportOutcome(
                WorkDigestTransportOutcomeKind.DefiniteTransientRejection,
                "smtp_transient_rejection");
        if (statusCode is >= 500 and < 600)
            return new WorkDigestTransportOutcome(
                WorkDigestTransportOutcomeKind.DefinitePermanentRejection,
                "smtp_permanent_rejection");
        return new WorkDigestTransportOutcome(WorkDigestTransportOutcomeKind.Unknown,
            "transport_ambiguous");
    }
}
