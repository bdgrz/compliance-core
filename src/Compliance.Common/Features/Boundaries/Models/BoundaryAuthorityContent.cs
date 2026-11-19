using Cntryl.Portia;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Boundaries.Models;

public sealed record BoundaryAuthorityContent(BoundaryAuthorityHeader Header,
    IReadOnlyList<ResponsibilityAssignmentView> Assignments, IReadOnlyList<BoundaryDecisionConstraint> DecisionConstraints)
{
    public IReadOnlyList<Uuid> DraftAuthorMemberIds { get; init; } = [];
}
