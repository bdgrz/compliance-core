using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
namespace Bdgrz.Compliance.Features.AuditPackages.Contracts;

/// <summary>Marks typed internal effects whose handlers independently verify the exact committed source.</summary>
public interface IAuditPackageReceiver : IRequestBase;
