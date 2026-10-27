using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Acquires the operational process name claim a change needs, or re-pins the claim the record already holds.</summary>
[Discriminator("bdgrz.operational-process-name-claim.claim-operational-process-name", 1)]
public sealed record ClaimOperationalProcessName(Uuid CommandId, SourceFactProof<OperationalProcessChangeAccepted> Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid RecordId,
    string NormalizedName, string NameKind) : INamedRecordReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
