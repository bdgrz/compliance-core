using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Examinations.Models;

sealed record ApplicationRetirementSourceContent(Uuid TenantId, Uuid ApplicationId,
    long Revision, DateTimeOffset EffectiveAt, string Reason, Uuid? MergedIntoApplicationId,
    long? MergedIntoApplicationRevision, Uuid LastChangedByMemberId,
    string LastChangedByDisplay, ActorReference Actor, DateTimeOffset LastChangedAt);
