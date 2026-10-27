using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Commands;

/// <summary>
///     Asserts that an exact approved control version treats the risk under <c>mitigate</c>. The assertion counts only
///     after an independent review accepts it. <paramref name="CommandId" /> is the treatment identity and
///     <paramref name="ExpectedRevision" /> the revision of the (risk, control) assertion line, zero for a new line.
///     HTTP-only.
/// </summary>
[Discriminator("bdgrz.risk.control_treatment.propose", 2)]
public sealed record ProposeRiskControlTreatment(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long ExpectedRevision, Uuid ControlId, Uuid ControlVersionId, string Rationale)
    : IRequest<RiskControlTreatmentRegistration>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
