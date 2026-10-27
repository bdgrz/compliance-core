using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Readers;

public interface IUserIdentityDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(CancellationToken ct = default);

    ValueTask<UserIdentityDirectoryPage> ListAsync(Uuid userId, int limit,
        string? cursor, CancellationToken ct = default);
}
