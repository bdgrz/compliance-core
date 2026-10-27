using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>An attributed decision on one immutable management assertion version.</summary>
public sealed record ManagementAssertionApprovalView(
    Uuid ApprovalId,
    int AssertionVersion,
    string Outcome,
    string Rationale,
    Uuid AssertionAuthorMemberId,
    Uuid ApprovedByMemberId,
    string ApproverRole,
    string ApproverDisplay,
    DateTimeOffset ApprovedAt);
