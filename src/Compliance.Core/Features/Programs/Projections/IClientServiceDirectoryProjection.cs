using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Programs.Projections;

public interface IClientServiceDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
