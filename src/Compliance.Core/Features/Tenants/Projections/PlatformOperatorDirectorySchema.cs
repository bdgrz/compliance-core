using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Projections;

static class PlatformOperatorDirectorySchema
{
    public static readonly KvDirectory<PlatformOperatorDirectoryRow, Uuid> Directory = new(
        "operators-by-user", ComplianceCoreJsonContext.Default.PlatformOperatorDirectoryRow,
        static row => row.UserId, static userId => [userId.ToString()], []);
}
