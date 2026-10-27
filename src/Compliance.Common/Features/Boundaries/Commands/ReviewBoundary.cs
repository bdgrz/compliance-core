using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
namespace Bdgrz.Compliance.Features.Boundaries.Commands;

[Discriminator("bdgrz.boundary.review", 1)]
public sealed record ReviewBoundary(Uuid CommandId, Uuid TenantId, Uuid BoundaryId, Uuid DraftVersionId,
    long ExpectedRevision, string Outcome, string Rationale, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest, IBoundaryAuthoringRequest, IBoundaryProgramResourceRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
