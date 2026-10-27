using Cntryl.Portia;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Projections;

/// <summary>Materializes team-member events into the queryable team-member directory.</summary>
public interface ITeamMemberDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
