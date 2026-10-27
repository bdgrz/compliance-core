using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Snapshots.Projections;

public interface ISnapshotDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
