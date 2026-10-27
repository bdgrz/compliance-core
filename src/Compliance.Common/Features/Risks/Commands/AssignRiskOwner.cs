using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Commands;

/// <summary>
///     Assigns the governed workforce person accountable for the risk; HTTP-only. <paramref name="ExpectedRevision" />
///     is the revision of the risk's governance owner, which counts owner assignments and reassessment triggers.
/// </summary>
[Discriminator("bdgrz.risk.owner.assign", 2)]
public sealed record AssignRiskOwner(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long ExpectedRevision, Uuid PersonId, string Rationale)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
