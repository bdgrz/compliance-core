using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>A control's current, pending, and historical operating plan versions.</summary>
public sealed record ControlOperatingPlanSetView(Uuid ControlId, long Revision,
    ControlOperatingPlanView? Current, ControlOperatingPlanView? Pending,
    IReadOnlyList<ControlOperatingPlanView> History);
