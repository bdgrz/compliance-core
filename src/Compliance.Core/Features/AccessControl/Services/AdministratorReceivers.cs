using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

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
