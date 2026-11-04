using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Remediation.Contracts;

public interface IWaiverManagementRequest : IRequestBase
{
    Uuid TenantId { get; }
}
