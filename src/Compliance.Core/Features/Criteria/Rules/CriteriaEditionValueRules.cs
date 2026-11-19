using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Rules;

/// <summary>Validation shared by a single-value target and its operation owner.</summary>
static class CriteriaEditionValueRules
{
    internal static bool Target(string? kind, string? field, string? category, string? code) => kind switch
    {
        "field" => (field is "coverage_note" or "source_url" or "content_rights" or "numbered_criteria_complete") &&
            category is null && code is null,
        "disclosure" => field is null && (category is "security" or "availability" or "confidentiality" or
            "processing_integrity" or "privacy") &&
            CriteriaContentBounds.Text(code, CriteriaContentBounds.DisclosureCodeBytes) && code == code.Trim(),
        _ => false,
    };

    internal static bool Shape(CriteriaEditionValueIntent? intent)
    {
        if (intent is null || intent.EditionId == Uuid.Empty || intent.DraftGeneration == Uuid.Empty ||
            intent.OperationId == Uuid.Empty || intent.ExpectedRevision < 0 ||
            !Target(intent.TargetKind, intent.Field, intent.Category, intent.Code))
            return false;
        return intent.TargetKind == "disclosure"
            ? intent.BooleanValue is null && CriteriaContentBounds.Text(intent.TextValue, CriteriaContentBounds.DisclosureNoteBytes)
            : intent.Field switch
            {
                "coverage_note" => intent.BooleanValue is null && CriteriaContentBounds.Text(intent.TextValue, CriteriaContentBounds.CoverageNoteBytes),
                "source_url" => intent.BooleanValue is null && CriteriaContentBounds.Text(intent.TextValue, CriteriaContentBounds.SourceUrlBytes),
                "content_rights" => intent.BooleanValue is null && CriteriaContentBounds.Text(intent.TextValue, CriteriaContentBounds.ContentRightsBytes),
                "numbered_criteria_complete" => intent.TextValue is null && intent.BooleanValue is not null,
                _ => false,
            };
    }

    internal static bool Admission(CriteriaEditionValueIntent intent, long generation, ActorAttribution actor,
        AccessAdmissionEvidence admission) => generation > 0 && Shape(intent) &&
        CriteriaContentBounds.AdmissionFields(actor, admission) &&
        AccessAdmissionEvidenceBinding.BindsCriteriaOperator(admission, admission.UserId, actor, intent.OperationId,
            CriteriaEditionValue.AddressOf(intent.EditionId, intent.TargetKind, intent.Field, intent.Category, intent.Code).ToString(),
            CriteriaEdition.BindAdmissionDigest(AuthoredIntentDigest.Compute(intent), generation));
}
