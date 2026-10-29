using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

static class Permissions
{
    public static string Normalize(string permission)
    {
        var normalized = permission.Trim().ToLowerInvariant();
        if (normalized.Length == 0)
            throw new ArgumentException("A permission is required.", nameof(permission));
        return normalized;
    }
}
