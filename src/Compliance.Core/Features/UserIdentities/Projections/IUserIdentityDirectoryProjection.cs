using Cntryl.Portia;
using Bdgrz.Compliance.Features.UserIdentities.Events;
namespace Bdgrz.Compliance.Features.UserIdentities.Projections;

public interface IUserIdentityDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(UserIdentityRegistered registered, CancellationToken ct = default);

    ValueTask ApplyAsync(UserIdentityAuthenticated authenticated, CancellationToken ct = default);

    ValueTask ApplyAsync(UserIdentityRevoked revoked, CancellationToken ct = default);
}
