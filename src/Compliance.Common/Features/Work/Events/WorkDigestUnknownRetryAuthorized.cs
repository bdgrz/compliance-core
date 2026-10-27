using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>A native operator admission bound to its exact unknown attempt and immutable original result.</summary>
[Discriminator("bdgrz.work.digest_dispatch.unknown_retry_authorized", 2)]
public sealed record WorkDigestUnknownRetryAuthorized(Uuid TenantId, Uuid MemberId,
    DateOnly WeekOf, Uuid MessageId, int PriorAttempt, Uuid AuthorizedBy,
    string EvidenceReference, string Rationale, DateTimeOffset AuthorizedAt) : DomainEvent, INativeOperationFact
{
    public OperationProvenance Operation { get; init; } = null!;
    public AuthorizeWorkDigestUnknownRetry Authored { get; init; } = null!;
    public ActorAttribution OriginalActor { get; init; } = null!;
    public ulong Revision { get; init; }
    public int MaximumAttempts { get; init; }
    public ImmutableContentRef Change { get; init; } = null!;
    public ImmutableContentRef ResponseContent { get; init; } = null!;
    public ImmutableContentRef? FirstResponse { get; init; }
    public ImmutableContentRef? OperationResult { get; init; }
}
