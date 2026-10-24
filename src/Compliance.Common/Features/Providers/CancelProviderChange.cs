using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Trusted process command that atomically cancels the exact pending provider change and fences its local delayed apply.
///     An already applied change recovers forward instead.
/// </summary>
[Discriminator("bdgrz.provider.cancel-provider-change", 1)]
public sealed record CancelProviderChange(Uuid CommandId, Uuid TenantId, Uuid RecordId, Uuid ChangeOperationId,
    long ExpectedRevision, ImmutableContentRef DefiniteFailureProof) : INamedRecordReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
