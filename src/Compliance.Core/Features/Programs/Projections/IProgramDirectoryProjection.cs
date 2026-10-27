using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Programs.Projections;

public interface IProgramDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
