using Cntryl.Portia;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.UserIdentities.Readers;

public interface IUserIdentityDirectoryReader
{
    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(CancellationToken ct = default);

    ValueTask<UserIdentityDirectoryPage> ListAsync(Uuid userId, int limit,
        string? cursor, CancellationToken ct = default);
}
