using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Risks.Commands;

/// <summary>
///     A personal acceptance by the acting member; HTTP-only, never an MCP tool. A risk owner
///     may accept their own risk only under an approved waiver scoped to
///     (risk, risk_id, residual_assessment_id, expected_revision, approve).
/// </summary>
[Discriminator("bdgrz.risk.accept", 1)]
public sealed record AcceptRisk(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long ExpectedRevision, Uuid ResidualAssessmentId, string ApproverAuthority,
    DateTimeOffset ExpiresAt, string Rationale, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<RiskAcceptanceView>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
