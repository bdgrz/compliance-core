using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Controls.Models;

/// <summary>Verified current-scope authority content, separate from immutable historical read reconstruction.</summary>
public sealed record ControlAuthorityContent(ControlAuthorityHeader Header,
    IReadOnlyList<ResponsibilityAssignmentView> Assignments,
    IReadOnlyList<ControlDecisionConstraint> DecisionConstraints,
    ControlPersonOwnerDesignationView? PersonOwner,
    ControlPersonOwner? PersonOwnerBinding = null,
    string? ApprovedContentSha256 = null,
    int? CurrentApplicabilityCount = null);
