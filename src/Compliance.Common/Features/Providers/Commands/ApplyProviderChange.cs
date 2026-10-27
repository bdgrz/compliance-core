using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Applies the accepted provider change once the names it needs are claimed.</summary>
[Discriminator("bdgrz.provider.apply-provider-change", 1)]
public sealed record ApplyProviderChange(Uuid CommandId, SourceFactProof<ProviderNameClaimed> Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid RecordId) : INamedRecordReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
