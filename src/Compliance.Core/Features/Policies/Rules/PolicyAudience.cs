using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Policies.Rules;

/// <summary>
///     The M0-D12 audience rules. Audiences are sets of workforce people, never platform members,
///     and are never inferred from application access, platform teams, or the IdP.
/// </summary>
public static class PolicyAudience
{
    public const string CoreSecurity = "core_security";
    public const string RoleTargeted = "role_targeted";

    public static string? Validate(string? kind, IReadOnlyList<string>? teams)
    {
        var clean = CleanTeams(teams);
        return kind switch
        {
            CoreSecurity when clean.Count == 0 => null,
            CoreSecurity => "A core_security audience names no roster teams.",
            RoleTargeted when clean.Count is >= 1 and <= 50 &&
                              clean.All(static team => team.Length <= 200) => null,
            RoleTargeted => "A role_targeted audience names 1 to 50 roster teams of at most 200 characters.",
            _ => "The audience kind must be core_security or role_targeted.",
        };
    }

    public static IReadOnlyList<string> CleanTeams(IReadOnlyList<string>? teams) =>
        (teams ?? []).Where(static team => !string.IsNullOrWhiteSpace(team))
        .Select(static team => team.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>
    ///     Whether a frozen work relationship is in the audience. <c>core_security</c> covers every
    ///     current employee and contractor; <c>role_targeted</c> covers every current worker whose
    ///     roster team (department) is named.
    /// </summary>
    public static bool Matches(string kind, IReadOnlyList<string> teams,
        FrozenWorkRelationship relationship)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        if (relationship.LifecycleStatus == "ended")
            return false;
        return kind == CoreSecurity
            ? relationship.WorkerType is "employee" or "contractor"
            : relationship.Department is { } department &&
              teams.Contains(department.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
