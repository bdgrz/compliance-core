using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>The permissions offered by the product; organization roles cannot invent new authority names.</summary>
static class ProductPermissionCatalog
{
    static readonly HashSet<string> Known = BuiltInRolePermissions.Create(Uuid.Empty, Uuid.Empty)
        .Select(static command => Permissions.Normalize(command.Permission))
        .Concat([
            RbacPermissions.EvidenceArtifactDisclose,
            RbacPermissions.RiskAcceptComplianceLead,
            RbacPermissions.RiskAcceptExecutive,
            RbacPermissions.ManagementExecutiveCeo,
            RbacPermissions.ManagementExecutiveCiso,
            "methodology.author",
            "methodology.approve",
        ])
        .ToHashSet(StringComparer.Ordinal);

    public static bool Contains(string permission) => Known.Contains(permission);
}
