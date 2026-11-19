using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Checks the exact admission binding of a source-verified participant intent.</summary>
static class CriteriaParticipantAdmission
{
    internal static bool AllowsLabel(DomainEvent intent) => intent switch
    {
        CriteriaEditionDraftRequested draft when draft.Admission is not null =>
            draft.Framework == "tsc" && draft.DraftGeneration == draft.OperationId && draft.SeedVersion is null &&
            draft.SeedFingerprint is null && Normalized(draft.Label) &&
            Matches(draft.EditionId, draft.OperationId, draft.ExecutionGeneration, draft.Actor, draft.Admission,
                AuthoredIntentDigest.Compute(new DraftCriteriaEdition(draft.EditionId, draft.OperationId, draft.Framework, draft.Label))),
        CriteriaEditionDraftRequested draft => IsSeedDraft(draft),
        CriteriaInitialLabelRequested initial => Normalized(initial.Label) &&
            Matches(initial.EditionId, initial.OperationId, initial.ExecutionGeneration, initial.Actor, initial.Admission,
                AuthoredIntentDigest.Compute(new ChooseInitialEditionLabel(initial.EditionId, initial.OperationId, initial.Label))),
        CriteriaEditionRenameRequested rename => Normalized(rename.OldLabel) && Normalized(rename.NewLabel) &&
            rename.OldLabel != rename.NewLabel && rename.OldLabelGeneration > 0 &&
            Matches(rename.EditionId, rename.OperationId, rename.ExecutionGeneration, rename.Actor, rename.Admission,
                AuthoredIntentDigest.Compute(new BeginCriteriaEditionRename(rename.EditionId, rename.OperationId, rename.NewLabel))),
        _ => false,
    };

    internal static bool AllowsHumanPublication(CriteriaPublicationStarted started) =>
        started.DraftGeneration != Uuid.Empty && started.AttemptOrdinal > 0 && started.LabelGeneration > 0 &&
        started.LabelConfirmationEventId != Uuid.Empty && Normalized(started.Label) &&
        !string.IsNullOrWhiteSpace(started.WitnessIdentifier) && started.WitnessIncarnationId != Uuid.Empty &&
        Matches(started.EditionId, started.PublicationAttemptId, started.ExecutionGeneration, started.Actor, started.Admission,
            AuthoredIntentDigest.Compute(new BeginCriteriaPublication(started.EditionId, started.PublicationAttemptId,
                started.WitnessIdentifier, started.WitnessIncarnationId)));

    static bool Matches(Uuid editionId, Uuid operationId, long generation, ActorAttribution actor,
        AccessAdmissionEvidence? admission, string authoredDigest) =>
        editionId != Uuid.Empty && operationId != Uuid.Empty && generation > 0 &&
        AccessAdmissionEvidenceBinding.IsCurrentFormat(admission) && admission is not null &&
        AccessAdmissionEvidenceBinding.BindsCriteriaOperator(admission, admission.UserId, actor, operationId,
            CriteriaEdition.AddressOf(editionId).ToString(), CriteriaEdition.BindAdmissionDigest(authoredDigest, generation));

    static bool Normalized(string label) => EditionLabelClaim.TryNormalize(label, out var normalized) && normalized == label;

    internal static bool IsSeedDraft(CriteriaEditionDraftRequested draft)
    {
        var seed = PlatformCriteriaCatalog.SeedEdition();
        var operation = PlatformCriteriaSeedDefinition.OperationOf(seed.EditionId, "edition");
        return draft.Admission is null && draft.EditionId == seed.EditionId && draft.DraftGeneration == operation &&
            draft.OperationId == operation && draft.ExecutionGeneration > 0 && draft.Framework == seed.Framework &&
            draft.Label == seed.EditionLabel && draft.Actor == new ActorAttribution("criteria-seed-bootstrap", "system") &&
            PlatformCriteriaSeedDefinition.MatchesBinding(draft.EditionId, draft.SeedVersion, draft.SeedFingerprint);
    }
}
