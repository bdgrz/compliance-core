using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Acquires the exact assurance report revision a personal review pinned at admission, so the report cannot be
///     revised until the review is recorded or canceled. <see cref="Source" /> is the accepted review request; the
///     receiver verifies it by hydrating the review owner.
/// </summary>
[Discriminator("bdgrz.provider-assurance-report.authorize-provider-review-use", 1)]
public sealed record AuthorizeProviderAssuranceReportProviderReviewUse(Uuid CommandId, NativeFactReference Source,
    EventStreamAddress Target, Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent,
    Uuid SourceOwnerId, Uuid SourceContentEventId, long SourceRevision, Uuid ProviderId, Uuid ReceivingOwnerId)
    : ISourceUseAuthorization, IProviderAssuranceReceiver
{
    /// <summary>The source revision in the shared use protocol; invalid negative input cannot match a held revision.</summary>
    [JsonIgnore]
    public ulong ExpectedContentRevision => SourceRevision >= 0 ? (ulong)SourceRevision : ulong.MaxValue;

    /// <summary>The receiving owner whose exact accepted operation this use serves.</summary>
    [JsonIgnore]
    public Uuid ReviewId => ReceivingOwnerId;

    Uuid IIdentifiedRequest.RequestId => CommandId;
}
