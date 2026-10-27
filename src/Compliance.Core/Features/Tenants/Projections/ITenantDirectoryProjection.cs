using Cntryl.Portia;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
namespace Bdgrz.Compliance.Features.Tenants.Projections;

/// <summary>Materializes tenant-registration events into the queryable tenant directory.</summary>
public interface ITenantDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
