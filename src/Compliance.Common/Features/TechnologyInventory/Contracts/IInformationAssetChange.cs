using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Contracts;

public interface IInformationAssetChange : ITechnologyInventoryWriteRequest, IIdentifiedRequest
{
    Uuid CommandId { get; }
    Uuid InformationAssetId { get; }
    long ExpectedRevision { get; }
}
