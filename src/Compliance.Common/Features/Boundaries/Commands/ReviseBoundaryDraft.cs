using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Models;
namespace Bdgrz.Compliance.Features.Boundaries.Commands;

[Discriminator("bdgrz.boundary.draft.revise", 1)]
public sealed record ReviseBoundaryDraft(Uuid CommandId, Uuid TenantId, Uuid BoundaryId, Uuid DraftVersionId,
    long ExpectedRevision, BoundaryContent Content)
    : IRequest, IBoundaryAuthoringRequest, IBoundaryProgramResourceRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
