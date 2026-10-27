using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Projections;

public interface IFindingClosureWorkItemProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
