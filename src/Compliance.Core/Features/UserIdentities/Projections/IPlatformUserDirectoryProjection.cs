using Cntryl.Portia;
using Bdgrz.Compliance.Features.UserIdentities.Events;
namespace Bdgrz.Compliance.Features.UserIdentities.Projections;

public interface IPlatformUserDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(UserIdentityRegistered registered, CancellationToken ct = default);
    ValueTask ApplyAsync(UserIdentityProfileObserved observed, CancellationToken ct = default);
}
