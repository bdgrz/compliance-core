using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     Records a governed, effective-dated data flow in the tenant's manual inventory. <paramref name="DataFlowId" />
///     is the stable identity of the flow and the logical identity of the creation; an exact retry replays the
///     original answer even after later versions. A flow carries one or more distinct information assets, with no
///     upper bound.
/// </summary>
[Discriminator("bdgrz.inventory.data_flow.record", 1)]
public sealed record RecordDataFlow(Uuid DataFlowId, Uuid TenantId, string SourceType, Uuid SourceId,
    string DestinationType, IReadOnlyList<Uuid> InformationAssetIds, string Purpose,
    bool EncryptedInTransit, bool EncryptedAtRest, DateOnly EffectiveFrom, Uuid OwnerPersonId,
    Uuid? DestinationId = null, string? DestinationParty = null,
    string? ExceptionReference = null)
    : IRequest<DataFlowRegistration>, ITechnologyInventoryWriteRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => DataFlowId;
}
