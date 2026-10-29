using Cntryl.Portia;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>An executive's immutable decision over one exact close validation.</summary>
public sealed record TypeIIPeriodCloseApprovalView(
    IReadOnlyList<Uuid> AcknowledgedMaterialItemIds,
    string Rationale,
    Uuid ApprovedByMemberId,
    string ApproverRole,
    string ApproverDisplay,
    DateTimeOffset ApprovedAt,
    long ValidatedRevision);
