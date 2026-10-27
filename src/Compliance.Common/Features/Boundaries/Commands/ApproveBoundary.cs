using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries.Commands;

[Discriminator("bdgrz.boundary.approve", 1)]
public sealed record ApproveBoundary(Uuid CommandId, Uuid TenantId, Uuid BoundaryId, Uuid DraftVersionId,
    long ExpectedRevision, Uuid AcceptedReviewDecisionId, DateOnly EffectiveFrom,
    string Rationale, string ImpactDigest, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest, IBoundaryAuthoringRequest, IBoundaryProgramResourceRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
