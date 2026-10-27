using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>The control version in force at the as-of time for one mapped control, or none when no version was effective.</summary>
public sealed record ReadinessEffectiveControl(Uuid ControlId, ControlVersionView? Version);
