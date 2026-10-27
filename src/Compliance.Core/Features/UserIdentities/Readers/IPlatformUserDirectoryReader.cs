using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Readers;

public interface IPlatformUserDirectoryReader
{
    ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default);
}
