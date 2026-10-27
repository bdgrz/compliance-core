using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies.Models;

public sealed record PolicyRetiredContent(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long Revision, long Version, Uuid DecisionId, Uuid AcceptedReviewDecisionId,
    DateOnly EffectiveUntil, string Rationale, ActorReference Actor, Uuid ActorMemberId,
    DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId) : IPolicyMutationContent;
