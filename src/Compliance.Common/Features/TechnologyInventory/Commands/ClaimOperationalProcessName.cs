using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Commands;

/// <summary>Acquires the operational process name claim a change needs, or re-pins the claim the record already holds.</summary>
[Discriminator("bdgrz.operational-process-name-claim.claim-operational-process-name", 1)]
public sealed record ClaimOperationalProcessName(Uuid CommandId, SourceFactProof<OperationalProcessChangeAccepted> Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid RecordId,
    string NormalizedName, string NameKind) : INamedRecordReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
