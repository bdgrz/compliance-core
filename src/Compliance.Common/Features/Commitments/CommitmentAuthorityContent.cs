using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed record CommitmentAuthorityContent(CommitmentAuthorityHeader Header, IReadOnlyList<Uuid> PendingAuthorMemberIds,
    IReadOnlyList<ResponsibilityAssignmentView> Assignments, IReadOnlyList<CommitmentDecisionConstraint> DecisionConstraints);
