using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Workforce.Contracts;

public interface IWorkforceRequest : IRequestBase
{
    Uuid TenantId { get; }
}
