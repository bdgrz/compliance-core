using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Boundaries.Projections;

public interface IBoundaryDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
