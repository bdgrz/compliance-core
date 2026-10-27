using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Commands;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Commands;

/// <summary>
///     Trusted process command that atomically cancels the exact pending operational process change and fences its local delayed apply.
///     An already applied change recovers forward instead.
/// </summary>
[Discriminator("bdgrz.operational-process.cancel-operational-process-change", 1)]
public sealed record CancelOperationalProcessChange(Uuid CommandId, Uuid TenantId, Uuid RecordId, Uuid ChangeOperationId,
    long ExpectedRevision, ImmutableContentRef DefiniteFailureProof) : INamedRecordReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
