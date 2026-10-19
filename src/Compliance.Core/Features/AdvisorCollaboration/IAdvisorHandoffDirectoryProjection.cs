using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

public interface IAdvisorHandoffDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
