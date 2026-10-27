using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed record BoundaryDecisionConstraint(Uuid MemberId, ResponsibilityType Type, DateTimeOffset At, Uuid? WaiverId);
