using Bdgrz.Compliance.Features.TechnologyInventory;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Commands;

/// <summary>
///     Revises an information asset. <paramref name="CommandId" /> is the logical identity of the operation, so an
///     exact retry replays its original answer.
/// </summary>
[Discriminator("bdgrz.inventory.information_asset.revise", 1)]
public sealed record ReviseInformationAsset(Uuid CommandId, Uuid TenantId, Uuid InformationAssetId,
    long ExpectedRevision, string Name, string Classification, string RetentionReference,
    Uuid OwnerPersonId, string Lifecycle, string? Description = null)
    : IRequest, ITechnologyInventoryWriteRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
