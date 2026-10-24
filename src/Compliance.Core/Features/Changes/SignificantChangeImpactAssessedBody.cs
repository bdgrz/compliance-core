using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Records the SignificantChangeImpactAssessedBody domain fact for one tenant-owned subject revision.</summary>
public sealed record SignificantChangeImpactAssessedBody(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    SignificantChangeView State,
    DateTimeOffset OccurredAt);
