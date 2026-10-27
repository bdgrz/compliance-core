using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

static class AuditPackageDirectorySchema
{
    // UpdatedAt is pinned to the creation fact in this identity-only directory and is never replaced by lifecycle data.
    public static readonly KvDirectoryIndex<AuditPackageSummaryView> ByProgramCreated = new(
        "by_program_created", 1, static package =>
        [
            package.ProgramId.ToString(),
            package.UpdatedAt.UtcTicks.ToString("D20", CultureInfo.InvariantCulture),
            package.AuditPackageId.ToString(),
        ]);

    public static readonly KvDirectory<AuditPackageSummaryView, Uuid> Packages = new("packages",
        ComplianceCoreJsonContext.Default.AuditPackageSummaryView, static package => package.AuditPackageId,
        static id => [id.ToString()], [ByProgramCreated]);
}
