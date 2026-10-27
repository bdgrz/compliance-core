using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Providers.Commands;

/// <summary>Releases the provider name claim the record's proven fact no longer needs.</summary>
[Discriminator("bdgrz.provider-name-claim.release-provider-name", 1)]
public sealed record ReleaseProviderName(Uuid CommandId, ReleaseProviderNameSourceProof Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid RecordId,
    string NormalizedName) : INamedRecordReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
