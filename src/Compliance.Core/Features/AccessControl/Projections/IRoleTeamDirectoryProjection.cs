using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Projections;

/// <summary>Materializes team-role events into the queryable role-team directory.</summary>
public interface IRoleTeamDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
