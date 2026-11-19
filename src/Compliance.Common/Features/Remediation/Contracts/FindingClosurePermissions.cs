namespace Bdgrz.Compliance.Features.Remediation.Contracts;

/// <summary>Lead permissions for proposing, reviewing and reopening governed finding closures.</summary>
public static class FindingClosurePermissions
{
    public const string Propose = "finding_closure.propose";
    public const string Review = "finding_closure.review";
    public const string Reopen = "finding_closure.reopen";
}
