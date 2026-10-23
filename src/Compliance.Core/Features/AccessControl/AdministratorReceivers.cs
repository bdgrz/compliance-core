namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>The receiver names of the administrator transition process, as the process catalog spells them.</summary>
public static class AdministratorReceivers
{
    public const string ApplyMemberWithdrawal = nameof(ApplyMemberWithdrawal);
    public const string ApplyAdministratorAssignmentWithdrawal = nameof(ApplyAdministratorAssignmentWithdrawal);
    public const string ApplyMemberRestoration = nameof(ApplyMemberRestoration);
    public const string ApplyAdministratorAssignmentRestoration = nameof(ApplyAdministratorAssignmentRestoration);
    public const string ApplyNeutralMemberSourceMutation = nameof(ApplyNeutralMemberSourceMutation);
    public const string ApplyNeutralAdministratorAssignmentMutation = nameof(ApplyNeutralAdministratorAssignmentMutation);
    public const string CompleteAdministratorWithdrawal = nameof(CompleteAdministratorWithdrawal);
    public const string CompleteAdministratorRestoration = nameof(CompleteAdministratorRestoration);
}
