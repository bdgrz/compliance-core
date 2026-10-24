namespace Bdgrz.Compliance.Features.Remediation;

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
