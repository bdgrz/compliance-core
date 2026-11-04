using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Contracts;

/// <summary>A complete asset snapshot carried by one named business fact.</summary>
public interface IInformationAssetFact : INativeOperationFact
{
    Uuid TenantId { get; }
    Uuid InformationAssetId { get; }
    long Revision { get; }
    InformationAssetContent Content { get; }
    ActorReference Actor { get; }
    DateTimeOffset ChangedAt { get; }
}
