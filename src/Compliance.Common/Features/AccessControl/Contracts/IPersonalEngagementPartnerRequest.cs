using Cntryl.Portia;
using Bdgrz.Compliance.Features.Exports.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Contracts;

/// <summary>Marks personal HTTP operations limited to a currently designated partner for one client.</summary>
public interface IPersonalEngagementPartnerRequest : IRequestBase
{
    Uuid TenantId { get; }
}
