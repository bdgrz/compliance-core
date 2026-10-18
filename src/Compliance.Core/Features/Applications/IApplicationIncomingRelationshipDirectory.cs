using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationIncomingRelationshipDirectory
{
    ValueTask<Page<ApplicationRelationshipView>> ListActiveIncomingAsync(Uuid tenantId,
        Uuid targetApplicationId, int limit, string? cursor, CancellationToken ct = default);

    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
}

public interface IApplicationIncomingRelationshipProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
