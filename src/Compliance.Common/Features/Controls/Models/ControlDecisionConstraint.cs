using Cntryl.Portia;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Controls.Models;

/// <summary>One local independence constraint retained only for the current pending scope.</summary>
public sealed record ControlDecisionConstraint(Uuid MemberId, ResponsibilityType Type, DateTimeOffset At, Uuid? WaiverId);
