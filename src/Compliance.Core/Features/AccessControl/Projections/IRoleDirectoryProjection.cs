using Cntryl.Portia;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Projections;

/// <summary>Materializes role-directory events into the queryable role directory.</summary>
public interface IRoleDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
