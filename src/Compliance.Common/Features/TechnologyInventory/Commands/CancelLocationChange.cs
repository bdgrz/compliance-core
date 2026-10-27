using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     Trusted process command that atomically cancels the exact pending location change and fences its local delayed apply.
///     An already applied change recovers forward instead.
/// </summary>
[Discriminator("bdgrz.location.cancel-location-change", 1)]
public sealed record CancelLocationChange(Uuid CommandId, Uuid TenantId, Uuid RecordId, Uuid ChangeOperationId,
    long ExpectedRevision, ImmutableContentRef DefiniteFailureProof) : INamedRecordReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
