using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed record PolicyReviewedContent(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long Revision, Uuid DecisionId, string Outcome, string Rationale, ActorReference Actor,
    Uuid ActorMemberId, DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId) : IPolicyMutationContent;
