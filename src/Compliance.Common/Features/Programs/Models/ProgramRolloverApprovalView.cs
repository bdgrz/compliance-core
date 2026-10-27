using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Models;

/// <summary>An independent approval or rejection of one exact carry-forward plan version.</summary>
public sealed record ProgramRolloverApprovalView(
    int Version,
    string Decision,
    string Rationale,
    string ContentSha256,
    Uuid ProposedByMemberId,
    Uuid ApprovedByMemberId,
    string ApproverRole,
    string ApproverDisplay,
    DateTimeOffset ApprovedAt);
