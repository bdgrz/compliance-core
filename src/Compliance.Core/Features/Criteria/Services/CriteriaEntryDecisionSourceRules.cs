using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

static class CriteriaEntryDecisionSourceRules
{
    internal static bool ValidAbandonment(CriteriaEditionDraftAbandoned value)
    {
        if (value.EditionId == Uuid.Empty || value.DraftGeneration == Uuid.Empty || value.OperationId == Uuid.Empty ||
            value.ExecutionGeneration <= 0 || !EditionLabelClaim.TryNormalize(value.Label, out var label) ||
            label != value.Label || value.LabelGeneration <= 0 || value.Admission is null ||
            !CriteriaContentBounds.AdmissionFields(value.Actor, value.Admission))
            return false;
        var digest = AuthoredIntentDigest.Compute(new AbandonCriteriaEditionDraft(value.EditionId, value.OperationId));
        return AccessAdmissionEvidenceBinding.BindsCriteriaOperator(value.Admission, value.Admission.UserId,
            value.Actor, value.OperationId, new CriteriaEdition(value.EditionId).Stream.ToString(),
            CriteriaEdition.BindAdmissionDigest(digest, value.ExecutionGeneration));
    }
}
