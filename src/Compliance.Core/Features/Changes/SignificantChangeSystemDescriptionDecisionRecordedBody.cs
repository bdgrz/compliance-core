using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Records an approved successor or no-change disposition for a significant change's description impact.</summary>
public sealed record SignificantChangeSystemDescriptionDecisionRecordedBody(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    SignificantChangeView State,
    DateTimeOffset OccurredAt);
