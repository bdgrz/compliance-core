using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Readers;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AuditPackages.Aggregates;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.AuditPackages.Projections;
namespace Bdgrz.Compliance.Features.AuditPackages.Projectors;

public sealed partial class AuditPackageDirectoryProjector(IAuditPackageDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(AuditPackageManifest.Area),
        FitzAuditPackageDirectory.ProjectorName), IProjectorHandler<AuditPackageCreated>
{
    public ValueTask HandleAsync(AuditPackageCreated fact, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(fact, ct);
}
