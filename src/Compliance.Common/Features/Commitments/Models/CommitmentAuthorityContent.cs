using Cntryl.Portia;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Commitments.Models;

public sealed record CommitmentAuthorityContent(CommitmentAuthorityHeader Header, IReadOnlyList<Uuid> PendingAuthorMemberIds,
    IReadOnlyList<ResponsibilityAssignmentView> Assignments, IReadOnlyList<CommitmentDecisionConstraint> DecisionConstraints);
