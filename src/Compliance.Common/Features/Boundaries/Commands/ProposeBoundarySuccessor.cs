using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Models;
namespace Bdgrz.Compliance.Features.Boundaries.Commands;

[Discriminator("bdgrz.boundary.successor.propose", 1)]
public sealed record ProposeBoundarySuccessor(Uuid CommandId, Uuid TenantId, Uuid BoundaryId,
    Uuid ExpectedApprovedVersionId, BoundaryContent Content, long ExpectedRevision)
    : IRequest<BoundaryRegistration>, IBoundaryAuthoringRequest, IBoundaryProgramResourceRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
