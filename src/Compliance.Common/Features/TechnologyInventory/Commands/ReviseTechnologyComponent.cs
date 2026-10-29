using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Commands;

/// <summary>
///     Revises a component. Category and system instance identity are immutable. <paramref name="CommandId" /> is the
///     logical identity of the operation, so an exact retry replays its original answer.
/// </summary>
[Discriminator("bdgrz.inventory.component.revise", 1)]
public sealed record ReviseTechnologyComponent(Uuid CommandId, Uuid TenantId, Uuid ComponentId,
    long ExpectedRevision, string Name, Uuid OwnerPersonId, string Lifecycle,
    string? EnvironmentReference = null, string? LocationReference = null,
    int? EndpointCount = null, string? ManagementSource = null)
    : IRequest, ITechnologyInventoryWriteRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
