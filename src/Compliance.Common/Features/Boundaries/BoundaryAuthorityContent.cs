using Bdgrz.Compliance.Features.Responsibilities;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed record BoundaryAuthorityContent(BoundaryAuthorityHeader Header,
    IReadOnlyList<ResponsibilityAssignmentView> Assignments, IReadOnlyList<BoundaryDecisionConstraint> DecisionConstraints);
