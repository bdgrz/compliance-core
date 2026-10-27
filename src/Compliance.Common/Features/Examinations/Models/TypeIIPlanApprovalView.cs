using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Executive approval of one reviewed plan version with retained rationale.</summary>
public sealed record TypeIIPlanApprovalView(
    int PlanVersion,
    string Rationale,
    Uuid ApprovedByMemberId,
    string ApproverRole,
    string ApproverDisplay,
    DateTimeOffset ApprovedAt);
