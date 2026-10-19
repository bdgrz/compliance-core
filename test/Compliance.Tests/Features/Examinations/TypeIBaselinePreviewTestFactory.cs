using Bdgrz.Compliance.Features.Examinations;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

static class TypeIBaselinePreviewTestFactory
{
    public static TypeIBaselinePreview Create(Cntryl.Portia.Uuid tenantId,
        Cntryl.Portia.Uuid programId, Cntryl.Portia.Uuid examinationEngagementId,
        IReadOnlyList<TypeIBaselineSourceReference> sources,
        IReadOnlyList<string>? blockers = null, IReadOnlyList<string>? scopeDifferences = null)
    {
        var coverage = TypeIBaselineSourceFamilies.Required.Select(family =>
                TypeIBaselineContentIdentity.Coverage(family,
                    sources.Where(source => SourceFamily(source.Kind) == family).ToArray()))
            .ToArray();
        return TypeIBaselinePreview.Create(tenantId, programId, examinationEngagementId,
            sources, blockers, scopeDifferences, coverage);
    }

    static string? SourceFamily(string kind) => kind switch
    {
        "approved_boundary" => "approved_boundary",
        "workforce_roster_snapshot" => "workforce",
        "application_revision" or "application_retirement" => "applications",
        "reviewed_system_revision" or "reviewed_system_retirement" => "reviewed_systems",
        "system_component_revision" => "system_components",
        "information_asset_revision" => "information_assets",
        "data_flow_revision" => "data_flows",
        "commitment_version" or "commitment_retirement" => "commitments",
        "system_requirement_version" or "system_requirement_retirement" => "system_requirements",
        "control_criterion_mapping_version" => "control_criterion_mappings",
        "control_evaluation" => "control_evaluations",
        "criteria_selection" or "criteria_edition" => "criteria",
        "control" or "approved_control" => "controls",
        "criteria" => "criteria",
        "policy" => "policies",
        "risk" => "risks",
        "provider" or "vendor" => "providers",
        "finding" => "findings",
        _ => null,
    };
}
