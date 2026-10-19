using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Policies;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed record PolicyVersionSourceContent(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    string Identifier, long Version, long Revision, bool Major, PolicyContent Content,
    string ContentSha256, DateOnly EffectiveFrom, long? PredecessorVersion,
    Uuid ApprovalDecisionId, Uuid AcceptedReviewDecisionId, ActorReference ApprovedBy,
    DateTimeOffset ApprovedAt, string? ImpactDigest, Uuid? SeparationOfDutiesWaiverId,
    PolicyDecisionView ApprovalDecision, PolicyDecisionView AcceptedReviewDecision,
    IReadOnlyList<PolicyDecisionView> PeriodicReviews);
