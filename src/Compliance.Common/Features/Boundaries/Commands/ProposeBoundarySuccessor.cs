using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries.Commands;

[Discriminator("bdgrz.boundary.successor.propose", 1)]
public sealed record ProposeBoundarySuccessor(Uuid CommandId, Uuid TenantId, Uuid BoundaryId,
    Uuid ExpectedApprovedVersionId, BoundaryContent Content, long ExpectedRevision)
    : IRequest<BoundaryRegistration>, IBoundaryAuthoringRequest, IBoundaryProgramResourceRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
