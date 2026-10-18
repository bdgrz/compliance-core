using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationEvidenceSourceReferenceDirectory
{
    ValueTask<Page<ApplicationEvidenceSourceReferenceView>> ListByControlAsync(Uuid tenantId,
        Uuid controlId, int limit, string? cursor, CancellationToken ct = default);

    ValueTask<bool> HasUnresolvedReferencesAsync(Uuid tenantId,
        IReadOnlyCollection<Uuid> programIds, CancellationToken ct = default);

    ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default);
}

public interface IApplicationEvidenceSourceReferenceProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}

public interface IApplicationEvidenceSourceAccess
{
    ValueTask<bool> CanReadAsync(Uuid tenantId, Uuid userId, Uuid artifactId,
        CancellationToken ct = default);
}
