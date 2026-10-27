using Cntryl.Portia;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Projections;

/// <summary>Materializes team-role events into the queryable team-role directory.</summary>
public interface ITeamRoleDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
