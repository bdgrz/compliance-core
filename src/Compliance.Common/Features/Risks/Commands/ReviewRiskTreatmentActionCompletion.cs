using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Commands;

/// <summary>
///     Independently accepts or rejects the pending completion of a treatment action; HTTP-only. Admission freezes the
///     exact submission, its cited evidence and the exact separation-of-duties waiver proof (the submitter and the
///     accountable member may review only under a waiver whose proof matches
///     (risk_treatment_action, action_id, submission_id, expected_revision, review)) and is acknowledged with 202;
///     <c>RiskActionCompletionReviewProcess</c> then holds every cited evidence request and records the decision.
/// </summary>
[Discriminator("bdgrz.risk.treatment_action.completion.review", 2)]
public sealed record ReviewRiskTreatmentActionCompletion(Uuid CommandId, Uuid TenantId, Uuid ProgramId,
    Uuid RiskId, Uuid ActionId, long ExpectedRevision, string Outcome, string Rationale,
    Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<OperationAcceptedResponse>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
