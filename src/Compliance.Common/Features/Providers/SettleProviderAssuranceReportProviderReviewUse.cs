using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Settles the report use after the recorded review (irreversible decision) or after the cancellation fence
///     (canceled). <see cref="Source" /> is the terminal review fact; the receiver resolves it from the review owner
///     and never infers a cancellation from missing native state.
/// </summary>
[Discriminator("bdgrz.provider-assurance-report.settle-provider-review-use", 1)]
public sealed record SettleProviderAssuranceReportProviderReviewUse(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid SourceOwnerId, Uuid ProviderId, Uuid ReviewId, SourceUseSettlementDisposition Disposition)
    : ISourceUseSettlement, IProviderAssuranceReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
