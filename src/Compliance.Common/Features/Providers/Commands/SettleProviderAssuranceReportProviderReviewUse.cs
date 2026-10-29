using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Providers.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.Providers.Commands;

/// <summary>
///     Settles the report use after the recorded review (irreversible decision) or after the cancellation fence
///     (canceled). <see cref="Source" /> is the terminal review fact; the receiver resolves it from the review owner
///     and never infers a cancellation from missing native state.
/// </summary>
[Discriminator("bdgrz.provider-assurance-report.settle-provider-review-use", 1)]
public sealed record SettleProviderAssuranceReportProviderReviewUse(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid SourceOwnerId, Uuid ProviderId, Uuid ReceivingOwnerId, SourceUseSettlementDisposition Disposition)
    : ISourceUseSettlement, IProviderAssuranceReceiver
{
    /// <summary>The receiving owner whose exact accepted operation this use serves.</summary>
    [JsonIgnore]
    public Uuid ReviewId => ReceivingOwnerId;

    Uuid IIdentifiedRequest.RequestId => CommandId;
}
