using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Projections;

/// <summary>Materializes team-directory events into the queryable team directory.</summary>
public interface ITeamDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
