using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
namespace Bdgrz.Compliance.Features.Tenants.Projections;

static class TenantInvitationDirectorySchema
{
    public static readonly KvDirectoryIndex<TenantInvitationDirectoryEntry> ByEmail = new(
        "by_email", 1, static invitation => [invitation.EmailAddress]);

    public static readonly KvDirectory<TenantInvitationDirectoryEntry, string> Directory = new(
        "tenant-invitations", ComplianceCoreJsonContext.Default.TenantInvitationDirectoryEntry,
        static invitation => invitation.EmailAddress, static emailAddress => [emailAddress],
        [ByEmail]);
}
