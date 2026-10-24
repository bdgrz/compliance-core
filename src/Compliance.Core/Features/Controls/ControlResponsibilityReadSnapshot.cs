using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>Read-only source reconstruction for one exact responsibility scope.</summary>
public sealed class ControlResponsibilityReadSnapshot(Uuid tenantId, ResponsibilityScope scope,
    IReadOnlyList<ResponsibilityAssignmentView> assignments)
{
    public Uuid TenantId => tenantId;
    public ResponsibilityScope Scope => scope;
    public IReadOnlyList<ResponsibilityAssignmentView> ReadAssignments() => assignments;
}
