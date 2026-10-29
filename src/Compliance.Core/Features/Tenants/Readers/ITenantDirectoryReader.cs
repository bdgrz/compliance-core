using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Models;
namespace Bdgrz.Compliance.Features.Tenants.Readers;

/// <summary>Reads the materialized tenant directory.</summary>
public interface ITenantDirectoryReader
{
    ValueTask<TenantView?> GetAsync(Uuid tenantId, CancellationToken ct = default);
    ValueTask<Page<TenantView>> ListAsync(int limit, string? cursor, CancellationToken ct = default);
}
