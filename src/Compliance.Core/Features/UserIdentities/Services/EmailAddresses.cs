using System.Net.Mail;
using System.Text;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.UserIdentities.Services;

static class EmailAddresses
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = value?.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is 0 or > 254)
        {
            return false;
        }

        try
        {
            var parsed = new MailAddress(normalized);
            return string.Equals(parsed.Address, normalized, StringComparison.Ordinal) &&
                   normalized.Contains('@', StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
