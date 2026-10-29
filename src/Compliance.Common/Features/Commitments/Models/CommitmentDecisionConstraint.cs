using Cntryl.Portia;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Commitments.Models;

public sealed record CommitmentDecisionConstraint(Uuid MemberId, ResponsibilityType Type, DateTimeOffset At, Uuid? WaiverId);
