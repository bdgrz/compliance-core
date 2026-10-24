using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>One compact significant-change fact and its exact immutable local mutation.</summary>
[Discriminator("bdgrz.changes.impact-review-work-linked", 3)]
public sealed record ImpactReviewWorkLinked(Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId,
    SignificantChangeAuthorityHeader Header, ImmutableContentRef CurrentContent, ImmutableContentRef Change,
    long NativeSequence, int ChangeCount, OperationProvenance Operation, ImmutableContentRef FirstResponse,
    ImmutableContentRef OperationResult, DateTimeOffset OccurredAt) : DomainEvent, ISignificantChangeFact;
