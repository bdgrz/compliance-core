using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Commands;

/// <summary>
///     Revises a location. Its kind is immutable. <paramref name="CommandId" /> is the logical identity of the
///     operation and <paramref name="ExpectedRevision" /> the revision of the location the caller observed. The answer
///     is the fixed 202 acknowledgment while the name claim and the record fact follow.
/// </summary>
[Discriminator("bdgrz.inventory.location.revise", 1)]
public sealed record ReviseLocation(Uuid CommandId, Uuid TenantId, Uuid LocationId, long ExpectedRevision,
    string Name, Uuid OwnerPersonId, string Lifecycle, string? GeographyReference = null)
    : IRequest<OperationAcceptedResponse>, ITechnologyInventoryWriteRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
