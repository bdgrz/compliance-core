using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies.Models;

public sealed record PolicyApprovedContent(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long Revision, long Version, bool Major, Uuid DecisionId, Uuid AcceptedReviewDecisionId,
    string ContentSha256, DateOnly EffectiveFrom, long? PredecessorVersion,
    string? ImpactDigest, string Rationale, ActorReference Actor, Uuid ActorMemberId,
    DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId) : IPolicyMutationContent;
