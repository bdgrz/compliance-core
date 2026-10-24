using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Acquires the location name claim a change needs, or re-pins the claim the record already holds.</summary>
[Discriminator("bdgrz.location-name-claim.claim-location-name", 1)]
public sealed record ClaimLocationName(Uuid CommandId, SourceFactProof<LocationChangeAccepted> Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid RecordId,
    string NormalizedName, string NameKind) : INamedRecordReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
