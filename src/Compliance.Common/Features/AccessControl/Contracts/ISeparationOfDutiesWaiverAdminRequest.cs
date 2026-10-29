using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Contracts;

/// <summary>Marks waiver administration as an Org Admin operation.</summary>
public interface ISeparationOfDutiesWaiverAdminRequest : IRequestBase
{
    Uuid TenantId { get; }
}
