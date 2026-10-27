using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Projections;

public interface IRiskDraftDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
