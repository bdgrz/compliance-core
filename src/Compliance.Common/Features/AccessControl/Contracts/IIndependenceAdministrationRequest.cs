using Cntryl.Portia;
using Bdgrz.Compliance.Features.Exports.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Contracts;

/// <summary>Client-owned service and evaluation records requiring explicit client administration grants.</summary>
public interface IIndependenceAdministrationRequest : IRequestBase
{
    Uuid TenantId { get; }
}
