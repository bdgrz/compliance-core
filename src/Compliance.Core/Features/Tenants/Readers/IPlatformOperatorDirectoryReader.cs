using Bdgrz.Compliance.Features.Tenants.Models;
using Cntryl.Fitz.Extensions;

namespace Bdgrz.Compliance.Features.Tenants.Readers;

public interface IPlatformOperatorDirectoryReader
{
    ValueTask<Page<PlatformOperatorDirectoryEntry>> ListAsync(int limit, string? cursor,
        CancellationToken ct = default);
}
