using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Boundaries.Commands;

[Discriminator("bdgrz.boundary.create", 1)]
public sealed record CreateBoundary(Uuid BoundaryId, Uuid TenantId, Uuid ProgramId, BoundaryContent Content)
    : IRequest<BoundaryRegistration>, IBoundaryAuthoringRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => BoundaryId;
}
