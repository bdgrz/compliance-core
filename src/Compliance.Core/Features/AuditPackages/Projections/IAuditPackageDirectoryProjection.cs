using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

public interface IAuditPackageDirectoryProjection : IProjectionStore
{
    ValueTask ApplyAsync(AuditPackageCreated fact, CancellationToken ct = default);
}
