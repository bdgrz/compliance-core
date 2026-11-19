using Microsoft.Extensions.Configuration;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.UserIdentities.Models;

public sealed record EmailChallengeDeliverySettings(
    string Mode, string? SmtpHost, int SmtpPort, string? FromAddress,
    string? Username, string? Password)
{
    public static EmailChallengeDeliverySettings FromConfiguration(IConfiguration configuration,
        bool requireRealDelivery)
    {
        var section = configuration.GetSection("Compliance:EmailDelivery");
        var mode = section["Mode"]?.Trim().ToLowerInvariant() ?? "mock";
        if (requireRealDelivery && mode is not ("ses" or "smtp"))
            throw new InvalidOperationException("Production email challenge delivery must be configured.");
        if (mode == "mock" && !requireRealDelivery)
            return new EmailChallengeDeliverySettings(mode, null, 0, null, null, null);
        if (mode is not ("ses" or "smtp"))
            throw new InvalidOperationException("Unsupported email challenge delivery mode.");

        var host = mode == "smtp" ? section["Smtp:Host"] : null;
        var from = mode == "ses" ? section["FromAddress"] : section["Smtp:FromAddress"];
        var username = mode == "smtp" ? section["Smtp:Username"] : null;
        var password = mode == "smtp" ? section["Smtp:Password"] : null;
        var port = 0;
        if (mode == "smtp" && (string.IsNullOrWhiteSpace(host) ||
            !int.TryParse(section["Smtp:Port"], out port) || port is < 1 or > 65535 ||
            string.IsNullOrEmpty(username) != string.IsNullOrEmpty(password)))
            throw new InvalidOperationException("SMTP email challenge delivery configuration is incomplete.");
        if (mode == "ses" && string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("Amazon SES email delivery requires a from address.");
        if (!System.Net.Mail.MailAddress.TryCreate(from, out _))
            throw new InvalidOperationException("Email sender address is invalid.");
        return new EmailChallengeDeliverySettings(mode, host, port, from, username, password);
    }
}
