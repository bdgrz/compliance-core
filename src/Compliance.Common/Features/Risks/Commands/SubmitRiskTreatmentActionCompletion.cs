using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Risks.Commands;

/// <summary>
///     Reports that a treatment action reached its target state, citing at least one fulfilled program evidence
///     request. The action stays incomplete until an independent review accepts the exact submission.
///     <paramref name="CommandId" /> is the submission identity. HTTP-only.
/// </summary>
[Discriminator("bdgrz.risk.treatment_action.completion.submit", 2)]
public sealed record SubmitRiskTreatmentActionCompletion(Uuid CommandId, Uuid TenantId, Uuid ProgramId,
    Uuid RiskId, Uuid ActionId, long ExpectedRevision, string Summary,
    IReadOnlyList<Uuid> EvidenceRequestIds)
    : IRequest<RiskTreatmentActionCompletionRegistration>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
