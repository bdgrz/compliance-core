namespace Bdgrz.Compliance.Features.AccessControl;

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
