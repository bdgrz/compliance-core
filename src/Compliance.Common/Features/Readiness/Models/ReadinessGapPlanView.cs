using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Models;

/// <summary>
///     The accountable owner, target date, and action planned for a gap. <see cref="Revision" /> is the revision of the
///     gap plan after this plan; it is the expected revision of the next plan or annotation.
/// </summary>
public sealed record ReadinessGapPlanView(Uuid GapId, Uuid OwnerMemberId, DateOnly TargetDate,
    string Action, ActorReference PlannedBy, DateTimeOffset PlannedAt)
{
    /// <summary>The revision of the gap plan after this plan.</summary>
    public long Revision { get; init; }
}
