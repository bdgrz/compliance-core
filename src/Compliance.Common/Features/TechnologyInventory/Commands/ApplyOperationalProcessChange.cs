using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Commands;

/// <summary>Applies the accepted operational process change once the names it needs are claimed.</summary>
[Discriminator("bdgrz.operational-process.apply-operational-process-change", 1)]
public sealed record ApplyOperationalProcessChange(Uuid CommandId, ApplyOperationalProcessChangeSourceProof Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid RecordId) : INamedRecordReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
