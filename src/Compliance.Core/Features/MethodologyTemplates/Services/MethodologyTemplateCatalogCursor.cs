using System.Text;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Services;

/// <summary>Affines a shared platform catalogue cursor to the authorized request scope; the inner cursor stays opaque.</summary>
static class MethodologyTemplateCatalogCursor
{
    public static string? Wrap(Uuid tenant, Uuid program, string? cursor) => cursor is null ? null
        : Convert.ToBase64String(Encoding.UTF8.GetBytes($"{tenant}\n{program}\n{cursor}"));
    public static bool TryUnwrap(Uuid tenant, Uuid program, string? cursor, out string? inner)
    {
        inner = null;
        if (cursor is null)
            return true;
        if (cursor.Length > 16384)
            return false;
        try
        {
            var value = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(cursor));
            var prefix = $"{tenant}\n{program}\n";
            if (!value.StartsWith(prefix, StringComparison.Ordinal) || value.Length == prefix.Length)
                return false;
            inner = value[prefix.Length..];
            return true;
        }
        catch (FormatException) { return false; }
        catch (DecoderFallbackException) { return false; }
    }
}
