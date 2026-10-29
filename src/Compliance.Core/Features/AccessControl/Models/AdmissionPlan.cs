using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>The branch the guard chose for one admission and the guard generation its fact will carry.</summary>
public sealed record AdmissionPlan(AdministratorTransitionKind Kind, string? Reason, long Generation)
{
    /// <summary>True when the fact is a withdrawal acceptance.</summary>
    public bool IsWithdrawal => Kind is AdministratorTransitionKind.SuspendMember or
        AdministratorTransitionKind.DeprovisionMember or AdministratorTransitionKind.RemoveAdministratorsAssignment;

    /// <summary>True when the fact is a restoration or registration acceptance.</summary>
    public bool IsRestoration => Kind is AdministratorTransitionKind.ReinstateMember or
        AdministratorTransitionKind.RestoreAdministratorsAssignment or
        AdministratorTransitionKind.RegisterAdministratorsAssignment;
}
