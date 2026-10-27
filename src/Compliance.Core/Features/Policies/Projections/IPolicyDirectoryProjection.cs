using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Policies.Projections;

public interface IPolicyDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
