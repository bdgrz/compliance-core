using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed record PolicyRetirementSourceContent(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    Uuid DecisionId, long Revision, long Version, string Outcome, string Rationale,
    ActorReference Actor, DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId,
    DateOnly EffectiveUntil);
