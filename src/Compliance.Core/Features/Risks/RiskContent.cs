namespace Bdgrz.Compliance.Features.Risks;

/// <summary>The schema names of the immutable content referenced by risk governance, treatment and action facts.</summary>
static class RiskContent
{
    public const string OwnerSchema = "RiskOwnerAssigned.OwnerContent.v1";
    public const string TriggerSchema = "RiskReassessmentTriggered.TriggerContent.v1";
    public const string TreatmentSchema = "RiskControlTreatmentProposed.TreatmentContent.v1";
    public const string ActionSchema = "RiskTreatmentActionAdded.ActionContent.v1";
    public const string RevisedEvidenceSchema = "RiskTreatmentActionRevised.EvidenceRequestIdsManifest.v1";
    public const string SubmittedEvidenceSchema = "RiskTreatmentActionCompletionSubmitted.EvidenceRequestIdsManifest.v1";
    public const string AuthoredReviewSchema = "ReviewRiskTreatmentActionCompletion.AuthoredReview.v1";
    public const string ReviewedEvidenceSchema = "risk-action-reviewed-evidence.v1";
    public const string EvidenceFactsSchema = "EvidenceRequest.ActionCompletionFacts.v1";
    public const string ProofsSchema = "RiskTreatmentActionCompletionReviewed.EvidenceUseProofs.v1";
    public const string FailureSchema = "RiskTreatmentActionCompletionReviewCanceled.FailureProof.v1";
    public const string WaiverProofSchema = "separation-of-duties-waiver-proof.v1";
    public const string ControlTreatmentRegistrationSchema = "Risk.RiskControlTreatmentRegistration.v1";
    public const string ActionRegistrationSchema = "Risk.RiskTreatmentActionRegistration.v1";
    public const string CompletionRegistrationSchema = "Risk.RiskTreatmentActionCompletionRegistration.v1";
}
