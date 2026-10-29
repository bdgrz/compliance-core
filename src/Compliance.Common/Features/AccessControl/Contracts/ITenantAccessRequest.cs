using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Contracts;

/// <summary>Marks a request as a read scoped to one tenant, requiring only the tenant-access permission.</summary>
public interface ITenantAccessRequest : IRequestBase
{
    Uuid TenantId { get; }
}
