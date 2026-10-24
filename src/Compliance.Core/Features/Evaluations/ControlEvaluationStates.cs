namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>The wire vocabulary of evaluation lifecycle, results, conclusions, deviations and retest status.</summary>
public static class ControlEvaluationStates
{
    public const string InProgress = "in_progress";
    public const string Submitted = "submitted";
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
    public const string ChangesRequested = "changes_requested";
    public const string Met = "met";
    public const string NotMet = "not_met";
    public const string NotTested = "not_tested";
    public const string Effective = "effective";
    public const string EffectiveWithExceptions = "effective_with_exceptions";
    public const string Ineffective = "ineffective";
    public const string Minor = "minor";
    public const string Material = "material";
    public const string Corrected = "corrected";
    public const string AcceptedWithWaiver = "accepted_with_waiver";
    public const string PendingDisposition = "pending_disposition";
    public const string Dispositioned = "dispositioned";
    public const string PendingRouting = "pending_routing";
    public const string RoutedToFinding = "routed_to_finding";
    public const string NotRequired = "not_required";
    public const string Required = "required";
    public const string Passed = "passed";
    public const string Failed = "failed";

    /// <summary>The three assertions every submission concludes exactly once.</summary>
    public static readonly IReadOnlyList<string> Assertions =
        ["design", "implementation", "evidence_sufficiency"];
}
