using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The complete server-side evidence an acceptance or staff assignment relied on at admission, retained as
///     immutable content. Nothing in it is supplied by a caller as policy: every item was read from its authoritative
///     owner. <paramref name="Proof" /> and <paramref name="Rules" /> are set for an acceptance;
///     <paramref name="AssignedStaff" /> for a positive staff assignment.
/// </summary>
public sealed record EngagementAcceptanceEvidence(ServiceEngagementView Engagement,
    IReadOnlyList<NonattestServiceView> Services, FirmStaffMemberView PartnerStaff,
    FirmProfessionalDutyDesignationView PartnerDesignation, long PartnerDutyContinuityStartRevision,
    string DecisionCode, string EvaluationOutcome, IReadOnlyList<Uuid> ConsideredServiceRecordIds,
    VerifiedEngagementAcceptance? Proof, IndependenceRuleVersionView? Rules, FirmStaffMemberView? AssignedStaff);
