using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
namespace Bdgrz.Compliance.Features.AccessControl;

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
