using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;

namespace Bdgrz.Compliance.Features.Boundaries.Commands;

[Discriminator("bdgrz.boundary.draft", 1)]
public sealed record DraftBoundary(Uuid CommandId, Uuid TenantId, Uuid ProgramId, long ExpectedRevision, BoundaryContent Content)
    : IRequest<BoundaryRegistration>, IBoundaryAuthoringRequest, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
