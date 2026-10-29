using Cntryl.Portia;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Commitments.Projections;

public sealed class CommitmentResponsibilityReadSnapshot(Uuid tenantId, ResponsibilityScope scope,
    IReadOnlyList<ResponsibilityAssignmentView> assignments)
{
    public Uuid TenantId { get; } = tenantId;
    public ResponsibilityScope Scope { get; } = scope;
    public IReadOnlyList<ResponsibilityAssignmentView> ReadAssignments() => assignments;
}
