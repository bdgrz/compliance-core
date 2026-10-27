using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class CommitmentResponsibilityReadSnapshot(Uuid tenantId, ResponsibilityScope scope,
    IReadOnlyList<ResponsibilityAssignmentView> assignments)
{
    public Uuid TenantId { get; } = tenantId;
    public ResponsibilityScope Scope { get; } = scope;
    public IReadOnlyList<ResponsibilityAssignmentView> ReadAssignments() => assignments;
}
