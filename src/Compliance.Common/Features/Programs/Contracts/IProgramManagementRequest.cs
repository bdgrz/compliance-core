using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Programs.Contracts;

public interface IProgramManagementRequest : IRequestBase
{
    Uuid TenantId { get; }
}
