using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed record ApplicationRetirementSourceContent(Uuid TenantId, Uuid ApplicationId,
    long Revision, DateTimeOffset EffectiveAt, string Reason, Uuid? MergedIntoApplicationId,
    long? MergedIntoApplicationRevision, Uuid LastChangedByMemberId,
    string LastChangedByDisplay, ActorReference Actor, DateTimeOffset LastChangedAt);
