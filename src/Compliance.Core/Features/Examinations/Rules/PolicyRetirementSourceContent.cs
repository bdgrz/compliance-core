using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Examinations.Rules;

sealed record PolicyRetirementSourceContent(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    Uuid DecisionId, long Revision, long Version, string Outcome, string Rationale,
    ActorReference Actor, DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId,
    DateOnly EffectiveUntil);
