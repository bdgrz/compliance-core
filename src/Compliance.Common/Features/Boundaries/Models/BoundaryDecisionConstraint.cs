using Cntryl.Portia;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Boundaries.Models;

public sealed record BoundaryDecisionConstraint(Uuid MemberId, ResponsibilityType Type, DateTimeOffset At, Uuid? WaiverId);
