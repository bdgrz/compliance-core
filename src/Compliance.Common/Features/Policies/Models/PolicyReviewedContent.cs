using Cntryl.Portia;
using Bdgrz.Compliance.Features.Policies.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Policies.Models;

public sealed record PolicyReviewedContent(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long Revision, Uuid DecisionId, string Outcome, string Rationale, ActorReference Actor,
    Uuid ActorMemberId, DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId) : IPolicyMutationContent;
