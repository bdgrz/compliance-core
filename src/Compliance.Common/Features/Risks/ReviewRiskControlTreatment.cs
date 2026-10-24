using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Independently accepts or rejects a proposed control treatment assertion; HTTP-only. The proposer may review
///     only under an approved waiver whose exact proof matches
///     (risk_control_treatment, treatment_id, control_version_id, expected_revision, review).
///     <paramref name="ExpectedRevision" /> is the revision of the assertion line.
/// </summary>
[Discriminator("bdgrz.risk.control_treatment.review", 2)]
public sealed record ReviewRiskControlTreatment(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    Uuid TreatmentId, long ExpectedRevision, string Outcome, string Rationale,
    Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
