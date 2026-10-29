using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Controls.Models;

/// <summary>Compact current authority; full content, assignments and history are immutable verified content.</summary>
public sealed record ControlAuthorityHeader(Uuid TenantId, Uuid ProgramId, Uuid ControlId, string Identifier,
    long Revision, bool IsCreated, bool IsVisible, bool IsApproved, bool IsRetired, bool HasOpenDraft,
    Uuid DraftVersionId, Uuid? PendingTargetId, Uuid? PendingRetirementId, DateOnly? PendingRetirementEffectiveUntil,
    Uuid? PendingAuthorMemberId, Uuid? AcceptedReviewDecisionId, Uuid? AcceptedReviewerMemberId,
    Uuid? LatestReviewDecisionId, DateTimeOffset? PendingDecisionChangedAt, int ApprovedVersionCount,
    Uuid? ApprovedVersionId, DateOnly? ApprovedEffectiveFrom, bool EverReviewed, bool ResponsibilityEverAssigned,
    ImmutableContentRef Content);
