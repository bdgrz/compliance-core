using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.Boundaries.Commands;
using Bdgrz.Compliance.Features.Commitments.Commands;
using Bdgrz.Compliance.Features.ControlMappings.Commands;
using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Policies.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
namespace Bdgrz.Compliance.Features.Work.Services;

/// <summary>Resolves the source commands behind queue actions; their mutation metadata controls the wall.</summary>
static class WorkSourceManagement
{
    internal static readonly IReadOnlyDictionary<string, Type[]> SourceRequests = new Dictionary<string, Type[]>
    {
        [WorkSource.ControlOccurrence] = [typeof(OpenControlOccurrence), typeof(AttestControlOccurrence), typeof(CorrectControlAttestation)],
        [WorkSource.OccurrenceReview] = [typeof(ReviewControlOccurrence)],
        [WorkSource.CorrectiveAction] = [typeof(CompleteCorrectiveAction)],
        [WorkSource.EvidenceRequest] = [typeof(FulfilEvidenceRequest)],
        [WorkSource.RiskTreatmentAction] = [typeof(SubmitRiskTreatmentActionCompletion)],
        [WorkSource.RiskTreatmentActionReview] = [typeof(ReviewRiskTreatmentActionCompletion)],
        [WorkSource.RiskControlTreatmentReview] = [typeof(ReviewRiskControlTreatment)],
        [WorkSource.ControlEvaluationReview] = [typeof(ReviewControlEvaluation)],
        [WorkSource.ControlOperatingPlanApproval] = [typeof(ApproveControlOperatingPlan)],
        [WorkSource.ControlCriterionMappingReview] = [typeof(ReviewControlCriterionMapping)],
        [WorkSource.CriterionApplicabilityReview] = [typeof(ReviewCriterionApplicability)],
        [WorkSource.AccessReviewReview] = [typeof(RecordAccessDecision)],
        [WorkSource.AccessReviewRemediation] = [typeof(RecordAccessRemediationChange)],
        [WorkSource.AdvisorFeedback] = [typeof(RespondToAdvisorFeedback), typeof(ResolveAdvisorFeedback)],
        [FindingClosureWork.Kind] = [typeof(CloseFinding)],
        [ControlDecisionWork.DraftReview] = [typeof(ReviewControl)],
        [ControlDecisionWork.DraftApproval] = [typeof(ApproveControl)],
        [ControlDecisionWork.RetirementReview] = [typeof(ReviewControl)],
        [ControlDecisionWork.RetirementApproval] = [typeof(RetireControl)],
        [PolicyDecisionWork.DraftReview] = [typeof(ReviewPolicyDraft)],
        [PolicyDecisionWork.DraftApproval] = [typeof(ApprovePolicy)],
        [PolicyDecisionWork.RetirementReview] = [typeof(ReviewPolicyDraft)],
        [PolicyDecisionWork.RetirementApproval] = [typeof(ApprovePolicyRetirement)],
        [PolicyDecisionWork.PeriodicReview] = [typeof(ConfirmPolicyReview)],
        [BoundaryDecisionWork.Review] = [typeof(ReviewBoundary)],
        [BoundaryDecisionWork.Approval] = [typeof(ApproveBoundary)],
        [CommitmentDecisionWork.Review] = [typeof(ReviewCommitmentDraft)],
        [CommitmentDecisionWork.Approval] = [typeof(ApproveCommitmentDraft)],
        [RiskAcceptanceWork.Kind] = [typeof(AcceptRisk)],
        [PolicyCampaignWork.Acknowledgement] = [typeof(AcknowledgePolicy)],
        [PolicyCampaignWork.TrainingCompletion] = [typeof(RecordTrainingCompletion)]
    };

    internal static bool IsManagementMutation(WorkCandidate candidate) =>
        SourceRequests.TryGetValue(candidate.Kind, out var requests) &&
        requests.Any(typeof(IClientManagementMutationRequest).IsAssignableFrom);
}
