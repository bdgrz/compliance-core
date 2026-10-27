using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Proposed or approved period plan, immutable versions, and independent decisions.</summary>
public sealed record TypeIIPlanView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid TypeIIPlanId,
    string Status,
    long Revision,
    int CurrentVersionNumber,
    TypeIIPlanVersionView? Current,
    IReadOnlyList<TypeIIPlanVersionView> Versions,
    IReadOnlyList<TypeIIPlanReviewView> Reviews,
    IReadOnlyList<TypeIIPlanApprovalView> Approvals,
    DateTimeOffset UpdatedAt);
