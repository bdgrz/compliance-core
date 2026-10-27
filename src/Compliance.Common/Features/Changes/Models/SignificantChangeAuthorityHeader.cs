using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes.Models;

/// <summary>Bounded current authority; full versions, assessments and governed references remain immutable content.</summary>
public sealed record SignificantChangeAuthorityHeader(Uuid TenantId, Uuid ProgramId, Uuid SignificantChangeId,
    long Revision, int CurrentVersionNumber, string ContentSha256, string Title, string Status,
    string HandlingClass, DateOnly EffectiveOn, DateOnly? EffectiveThrough, Uuid OwnerMemberId,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset UpdatedAt, bool PeriodCloseEligible);
