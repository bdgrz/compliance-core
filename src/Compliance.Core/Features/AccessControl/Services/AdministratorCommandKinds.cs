using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Canonical command kinds of the guard-first administrator transitions, and the reason each one withholds.</summary>
static class AdministratorCommandKinds
{
    public const string Suspend = "SuspendMember";
    public const string Deprovision = "DeprovisionMember";
    public const string Reinstate = "ReinstateMember";
    public const string Assign = "AssignTeamMember";
    public const string Remove = "RemoveTeamMember";

    /// <summary>The reason a withdrawing command withholds availability for, or null for other commands.</summary>
    public static string? WithdrawalReason(string commandKind) => commandKind switch
    {
        Suspend => TenantManagerGuard.Suspended,
        Deprovision => TenantManagerGuard.Deprovisioned,
        Remove => TenantManagerGuard.RemovedFromAdministrators,
        _ => null,
    };
}
