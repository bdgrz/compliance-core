using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Responsibilities;

namespace Bdgrz.Compliance.Features.Boundaries.Models;

public sealed record BoundaryAuthorityContent(BoundaryAuthorityHeader Header,
    IReadOnlyList<ResponsibilityAssignmentView> Assignments, IReadOnlyList<BoundaryDecisionConstraint> DecisionConstraints);
