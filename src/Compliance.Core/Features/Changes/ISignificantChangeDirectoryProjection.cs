using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

public interface ISignificantChangeDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
