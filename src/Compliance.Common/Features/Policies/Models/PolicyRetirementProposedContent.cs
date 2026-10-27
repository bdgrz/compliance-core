using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed record PolicyRetirementProposedContent(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long Revision, long Version, DateOnly EffectiveUntil, string Rationale,
    ActorReference Actor, Uuid ActorMemberId, DateTimeOffset ProposedAt) : IPolicyMutationContent;
