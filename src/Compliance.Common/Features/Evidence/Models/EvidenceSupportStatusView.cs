namespace Bdgrz.Compliance.Features.Evidence.Models;

/// <summary>Current lifecycle status alongside the unchanged original support assertion.</summary>
public sealed record EvidenceSupportStatusView(EvidenceSupportView Original, long Revision, bool IsActive,
    EvidenceSupportWithdrawalView? Withdrawal);
