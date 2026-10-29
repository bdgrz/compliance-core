using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Tenants.Services;

/// <summary>
///     Normalizes and validates tenant slugs against the rule the backlog defines for R1-15: 4-63
///     characters, starts with a letter, ends with a letter or digit, no consecutive hyphens, not a
///     reserved top-level route, and not shaped like a <see cref="Uuid" /> (so a slug can never
///     collide with the <c>tenant_id</c> identifier format).
/// </summary>
public static class TenantSlugs
{
    // Every top-level server or client route segment. Adding a new top-level route requires adding
    // it here first, per the backlog's registry-ownership rule.
    static readonly HashSet<string> ReservedRoutes = new(StringComparer.Ordinal)
    {
        "api", "auth", "health", "healthz", "openapi", "login", "logout", "signup", "callback",
        "select", "tenants", "organizations", "new", "create", "account", "settings", "admin",
        "portfolio", "work", "my-work", "invitations", "invite", "help", "support", "docs",
        "status", "static", "assets", "www", "app",
        "mcp", "developer-login", "teams", "roles",
    };

    internal static IEnumerable<string> ReservedRouteSegments => ReservedRoutes;

    public static bool IsReservedRoute(string segment) => ReservedRoutes.Contains(segment);

    public static bool TryNormalize(string? value, out string normalized) =>
        TryNormalize(value, out normalized, out _);

    public static bool TryNormalize(string? value, out string normalized, out string reason) =>
        TryNormalizeCore(value, out normalized, out reason, rejectReserved: true);

    internal static bool TryNormalizeForLookup(string? value, out string normalized) =>
        TryNormalizeCore(value, out normalized, out _, rejectReserved: false);

    static bool TryNormalizeCore(string? value, out string normalized, out string reason, bool rejectReserved)
    {
        normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        reason = string.Empty;
        if (normalized.Length is < 4 or > 63)
        {
            reason = "A slug must contain 4 to 63 characters.";
            return false;
        }
        if (normalized[0] is < 'a' or > 'z')
        {
            reason = "A slug must start with a letter.";
            return false;
        }
        if (normalized[^1] == '-')
        {
            reason = "A slug must end with a letter or digit.";
            return false;
        }

        var previousHyphen = false;
        foreach (var character in normalized)
        {
            var hyphen = character == '-';
            if (!(character is >= 'a' and <= 'z' or >= '0' and <= '9') && !hyphen ||
                hyphen && previousHyphen)
            {
                reason = hyphen && previousHyphen
                    ? "A slug cannot contain consecutive hyphens."
                    : "A slug may contain only lowercase letters, digits, and hyphens.";
                return false;
            }

            previousHyphen = hyphen;
        }

        if (rejectReserved && ReservedRoutes.Contains(normalized))
        {
            reason = "A slug cannot use a reserved route.";
            return false;
        }
        if (Uuid.TryParse(normalized, null, out _))
        {
            reason = "A slug cannot match a tenant_id.";
            return false;
        }

        return true;
    }
}
