using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Acquires the provider name claim a change needs, or re-pins the claim the record already holds.</summary>
[Discriminator("bdgrz.provider-name-claim.claim-provider-name", 1)]
public sealed record ClaimProviderName(Uuid CommandId, SourceFactProof<ProviderChangeAccepted> Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid RecordId,
    string NormalizedName, string NameKind) : INamedRecordReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
