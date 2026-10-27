using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies.Models;

/// <summary>Compact current gates; authored content, author sets and all version/decision history are immutable content.</summary>
public sealed record PolicyAuthorityHeader(Uuid TenantId, Uuid ProgramId, Uuid PolicyId, string Identifier,
    long Revision, bool IsCreated, bool IsVisible, bool IsRetired, bool HasOpenDraft,
    long? DraftPredecessorVersion, Uuid? AcceptedReviewDecisionId, Uuid? AcceptedReviewerMemberId,
    string? LatestReviewOutcome, long? CurrentVersion, DateOnly? CurrentVersionEffectiveFrom,
    long? PendingRetirementVersion, DateOnly? PendingRetirementEffectiveUntil, DateOnly? LastReviewedOn,
    bool EverReviewed, ImmutableContentRef Content);
