namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>The source families required for a complete Type I point-in-time baseline.</summary>
public static class TypeIBaselineSourceFamilies
{
    public static IReadOnlyList<string> Required { get; } = Array.AsReadOnly(new[]
    {
        "approved_boundary",
        "workforce",
        "applications",
        "reviewed_systems",
        "system_components",
        "information_assets",
        "data_flows",
        "commitments",
        "system_requirements",
        "criteria",
        "controls",
        "control_criterion_mappings",
        "control_evaluations",
        "policies",
        "policy_communications",
        "access_expectations",
        "access_review_campaigns",
        "risks",
        "providers",
        "evidence_governance",
        "findings",
    });
}
