using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Records the SignificantChangeRecordedBody domain fact for one tenant-owned subject revision.</summary>
public sealed record SignificantChangeRecordedBody(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    SignificantChangeView State,
    DateTimeOffset OccurredAt);
