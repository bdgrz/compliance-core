using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Controls.Projections;

/// <summary>Read-only source reconstruction for one exact responsibility scope.</summary>
public sealed class ControlResponsibilityReadSnapshot(Uuid tenantId, ResponsibilityScope scope,
    IReadOnlyList<ResponsibilityAssignmentView> assignments)
{
    public Uuid TenantId => tenantId;
    public ResponsibilityScope Scope => scope;
    public IReadOnlyList<ResponsibilityAssignmentView> ReadAssignments() => assignments;
}
