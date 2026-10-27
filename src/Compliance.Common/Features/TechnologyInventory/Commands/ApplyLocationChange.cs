using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Applies the accepted location change once the names it needs are claimed.</summary>
[Discriminator("bdgrz.location.apply-location-change", 1)]
public sealed record ApplyLocationChange(Uuid CommandId, ApplyLocationChangeSourceProof Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid RecordId) : INamedRecordReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
