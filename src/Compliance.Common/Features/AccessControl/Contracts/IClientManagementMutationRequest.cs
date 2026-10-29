using Cntryl.Portia;
using Bdgrz.Compliance.Features.Exports.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Contracts;

/// <summary>Explicitly identifies authoring or approval of a client's management record.</summary>
public interface IClientManagementMutationRequest : IRequestBase
{
    Uuid TenantId { get; }
}
