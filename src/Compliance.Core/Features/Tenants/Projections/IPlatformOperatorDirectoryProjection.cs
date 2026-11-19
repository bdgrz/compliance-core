using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Projections;

public interface IPlatformOperatorDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
