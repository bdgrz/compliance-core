using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AuditPackages.Events;
namespace Bdgrz.Compliance.Features.AuditPackages.Projections;

public interface IAuditPackageDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(AuditPackageCreated fact, CancellationToken ct = default);
}
