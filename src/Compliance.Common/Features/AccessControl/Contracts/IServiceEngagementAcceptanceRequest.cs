using Cntryl.Portia;
using Bdgrz.Compliance.Features.Exports.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Contracts;

/// <summary>Acceptance is a firm professional operation, separate from client administration.</summary>
public interface IServiceEngagementAcceptanceRequest : IRequestBase
{
    Uuid TenantId { get; }
}
