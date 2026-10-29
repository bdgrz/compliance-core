using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Commands;

/// <summary>Releases the operational process name claim the record's proven fact no longer needs.</summary>
[Discriminator("bdgrz.operational-process-name-claim.release-operational-process-name", 1)]
public sealed record ReleaseOperationalProcessName(Uuid CommandId, ReleaseOperationalProcessNameSourceProof Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid RecordId,
    string NormalizedName) : INamedRecordReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
