using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Marks typed internal effects whose handlers independently verify the exact committed source.</summary>
public interface IAuditPackageReceiver : IRequestBase;
