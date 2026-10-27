using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

public sealed partial class AuditPackageDirectoryProjector(IAuditPackageDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(AuditPackageManifest.Area),
        FitzAuditPackageDirectory.ProjectorName), IProjectorHandler<AuditPackageCreated>
{
    public ValueTask HandleAsync(AuditPackageCreated fact, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(fact, ct);
}
