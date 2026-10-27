using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Models;

/// <summary>Versioned Type II rollover, independent decision, and successor period receipt.</summary>
public sealed record ProgramRolloverView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid ProgramRolloverId,
    string Status,
    long Revision,
    ProgramRolloverVersionView CurrentVersion,
    IReadOnlyList<ProgramRolloverVersionView> Versions,
    IReadOnlyList<ProgramRolloverApprovalView> Approvals,
    Uuid? CreatedPeriodId,
    Uuid? CreatedTypeIIPlanId,
    DateTimeOffset? NextPeriodCreatedAt,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset UpdatedAt)
{
    public ProgramRolloverContent Content => CurrentVersion.Content;
    public string ContentSha256 => CurrentVersion.ContentSha256;
}
