using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.AccessControl.Projections;

/// <summary>The shared role-permission-directory schema, used by both the read and write sides.</summary>
static class RolePermissionDirectorySchema
{
    public static readonly KvDirectoryIndex<RolePermissionView> ByRole = new(
        "by_role", 1, static view => [view.RoleId.ToString(), view.Permission]);

    public static readonly KvDirectory<RolePermissionView, (Uuid RoleId, string Permission)> Directory = new(
        "role-permissions",
        ComplianceCoreJsonContext.Default.RolePermissionView,
        static view => (view.RoleId, view.Permission),
        static identity => [identity.RoleId.ToString(), identity.Permission],
        [ByRole]);
}
