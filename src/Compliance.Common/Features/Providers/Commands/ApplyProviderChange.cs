using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Events;
namespace Bdgrz.Compliance.Features.Providers.Commands;

/// <summary>Applies the accepted provider change once the names it needs are claimed.</summary>
[Discriminator("bdgrz.provider.apply-provider-change", 1)]
public sealed record ApplyProviderChange(Uuid CommandId, SourceFactProof<ProviderNameClaimed> Source, EventStreamAddress Target, Uuid OperationId,
    long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid RecordId) : INamedRecordReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
