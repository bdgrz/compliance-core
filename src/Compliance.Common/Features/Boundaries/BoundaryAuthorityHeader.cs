using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed record BoundaryAuthorityHeader(Uuid TenantId, Uuid ProgramId, Uuid BoundaryId,
    long Revision, bool IsCreated, bool IsVisible, Uuid DraftVersionId, long DraftRevision,
    Uuid DraftAuthorMemberId, bool DraftEverReviewed, Uuid AcceptedReviewDecisionId,
    string? LatestReviewOutcome, Uuid LatestApprovedVersionId, Uuid FirstApprovedVersionId,
    DateOnly? LatestApprovedEffectiveFrom, DateTimeOffset? DraftChangedAt, ImmutableContentRef Content);
