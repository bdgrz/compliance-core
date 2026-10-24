namespace Bdgrz.Compliance.Features.Operations;

/// <summary>The wire state names of operating plan versions and control occurrences.</summary>
public static class ControlOperationStates
{
    public const string PendingApproval = "pending_approval";
    public const string Approved = "approved";
    public const string Superseded = "superseded";
    public const string Expected = "expected";
    public const string Missed = "missed";
    public const string Open = "open";
    public const string Submitted = "submitted";
    public const string Returned = "returned";
    public const string ActionRequested = "action_requested";
    public const string Deferred = "deferred";
    public const string Complete = "complete";
    public const string Failed = "failed";
    public const string NotApplicable = "not_applicable";
    public const string Skipped = "skipped";
}
