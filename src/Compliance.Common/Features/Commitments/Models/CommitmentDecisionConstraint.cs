using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed record CommitmentDecisionConstraint(Uuid MemberId, ResponsibilityType Type, DateTimeOffset At, Uuid? WaiverId);
