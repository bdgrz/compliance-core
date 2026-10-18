using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>An executive's immutable decision over one exact close validation.</summary>
public sealed record TypeIIPeriodCloseApprovalView(
    IReadOnlyList<Uuid> AcknowledgedMaterialItemIds,
    string Rationale,
    Uuid ApprovedByMemberId,
    string ApproverRole,
    string ApproverDisplay,
    DateTimeOffset ApprovedAt,
    long ValidatedRevision);
