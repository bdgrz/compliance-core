using Bdgrz.Compliance.Features.AuditPackages.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Projections;

public interface IAuditPackageDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(AuditPackageCreated fact, CancellationToken ct = default);
}
