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
