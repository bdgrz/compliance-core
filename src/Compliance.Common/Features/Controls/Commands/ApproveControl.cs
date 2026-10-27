using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

[Discriminator("bdgrz.control.approve", 2)]
public sealed record ApproveControl(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long ExpectedRevision, Uuid AcceptedReviewDecisionId, DateOnly EffectiveFrom,
    string Rationale, Uuid? SeparationOfDutiesWaiverId = null, string? ImpactDigest = null)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
