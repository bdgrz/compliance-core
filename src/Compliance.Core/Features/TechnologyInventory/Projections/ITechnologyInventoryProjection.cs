using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Projections;

public interface ITechnologyInventoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
