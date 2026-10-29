using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.UserIdentities.Projections;

static class PlatformUserDirectorySchema
{
    public static readonly KvDirectory<PlatformUserDirectoryEntry, Uuid> Directory = new(
        "platform-users-by-id",
        ComplianceCoreJsonContext.Default.PlatformUserDirectoryEntry,
        static user => user.UserId,
        static userId => [userId.ToString()],
        []);
}
