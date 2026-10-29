using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessReviews.Projections;

public interface IAccessPopulationDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
