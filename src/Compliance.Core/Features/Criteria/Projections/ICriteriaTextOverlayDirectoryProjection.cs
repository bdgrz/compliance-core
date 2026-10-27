using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Projections;

public interface ICriteriaTextOverlayDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
