using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.AuditPackages.Projections;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Projectors;

public sealed partial class AuditPackageDirectoryProjector(IAuditPackageDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(AuditPackageManifest.Area),
        FitzAuditPackageDirectory.ProjectorName), IProjectorHandler<AuditPackageCreated>
{
    public ValueTask HandleAsync(AuditPackageCreated fact, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(fact, ct);
}
