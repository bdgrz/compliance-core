using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Applications.Contracts;

public interface IApplicationInventoryRequest : IRequestBase
{
    Uuid TenantId { get; }
}

/// <summary>
///     A read request whose handler applies restricted application visibility to every returned
///     resource. Scoped restricted-read grants may authorize these requests.
/// </summary>
public interface IRestrictedApplicationResourceReadRequest : IApplicationInventoryRequest
{
}

public interface IApplicationInventoryWriteRequest : IApplicationInventoryRequest, IClientManagementMutationRequest
{
}
