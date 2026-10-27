using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Remediation.Services;

/// <summary>The status vocabulary of findings and corrective actions, and the materiality mapping of severities.</summary>
public static class FindingStates
{
    public const string Open = "open";
    public const string Remediated = "remediated";
    public const string Closed = "closed";
    public const string Accepted = "accepted";
    public const string Overdue = "overdue";
    public const string Unresolved = "unresolved";
    public const string Completed = "completed";
    public const string RiskAcceptance = "risk_acceptance";
    public const string Waiver = "waiver";

    /// <summary>The work-queue materiality of a finding severity.</summary>
    public static string Materiality(string severity) => severity switch
    {
        "critical" or "high" => "high",
        "medium" => "medium",
        _ => "low",
    };
}
