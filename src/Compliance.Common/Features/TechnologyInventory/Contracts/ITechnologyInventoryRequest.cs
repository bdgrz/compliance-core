using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Contracts;

public interface ITechnologyInventoryRequest : IRequestBase
{
    Uuid TenantId { get; }
}
