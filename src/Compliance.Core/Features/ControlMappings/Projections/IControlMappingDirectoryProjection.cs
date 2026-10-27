using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.ControlMappings.Projections;

public interface IControlMappingDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
