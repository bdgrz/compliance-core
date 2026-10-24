using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     The admitted review was recorded as an immutable fact once its report use was held. The complete
///     <see cref="ProviderReviewContent" /> is immutable content; the provider revision and materiality are the
///     observed historical facts of the recording.
/// </summary>
[Discriminator("bdgrz.provider.review.recorded", 1)]
public sealed record ProviderReviewRecorded(Uuid TenantId, Uuid ReviewId, Uuid ProviderId, Uuid RequestId,
    ImmutableContentRef ContentContent, long ProviderRevision, string? ProviderMateriality,
    long? AssuranceReportRevision, ActorReference Actor, DateTimeOffset RecordedAt, OperationProvenance Operation,
    ImmutableContentRef Intent, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
