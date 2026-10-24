using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records the TypeIIPeriodCloseApprovedBody domain fact for one tenant-owned subject revision.</summary>
public sealed record TypeIIPeriodCloseApprovedBody(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TypeIIPeriodView State,
    DateTimeOffset OccurredAt);
