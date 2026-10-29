using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Models;
namespace Bdgrz.Compliance.Features.Tenants.Readers;

public interface ITenantInvitationDirectoryReader
{
    ValueTask<TenantInvitationDirectoryEntry?> GetAsync(Uuid tenantId, string emailAddress,
        CancellationToken ct = default);
    ValueTask<Page<TenantInvitationDirectoryEntry>> ListAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default);
}
