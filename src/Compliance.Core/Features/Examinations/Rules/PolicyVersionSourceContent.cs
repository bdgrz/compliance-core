using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Policies.Models;
namespace Bdgrz.Compliance.Features.Examinations.Rules;

sealed record PolicyVersionSourceContent(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    string Identifier, long Version, long Revision, bool Major, PolicyContent Content,
    string ContentSha256, DateOnly EffectiveFrom, long? PredecessorVersion,
    Uuid ApprovalDecisionId, Uuid AcceptedReviewDecisionId, ActorReference ApprovedBy,
    DateTimeOffset ApprovedAt, string? ImpactDigest, Uuid? SeparationOfDutiesWaiverId,
    PolicyDecisionView ApprovalDecision, PolicyDecisionView AcceptedReviewDecision,
    IReadOnlyList<PolicyDecisionView> PeriodicReviews);
