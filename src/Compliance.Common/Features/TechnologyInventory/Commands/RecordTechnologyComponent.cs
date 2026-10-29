using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Commands;

/// <summary>
///     Records a governed technology component in the tenant's manual inventory. <paramref name="ComponentId" /> is the
///     stable identity of the component and the logical identity of the creation; an exact retry replays the original
///     answer even after later revisions. A cloud account component shares its system instance identity.
/// </summary>
[Discriminator("bdgrz.inventory.component.record", 1)]
public sealed record RecordTechnologyComponent(Uuid ComponentId, Uuid TenantId, string Category, string Name,
    Uuid OwnerPersonId, string? EnvironmentReference = null, string? LocationReference = null,
    Uuid? SystemInstanceId = null, int? EndpointCount = null, string? ManagementSource = null)
    : IRequest<TechnologyComponentRegistration>, ITechnologyInventoryWriteRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => ComponentId;
}
