using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries.Commands;

[Discriminator("bdgrz.boundary.draft.discard", 1)]
public sealed record DiscardBoundaryDraft(Uuid CommandId, Uuid TenantId, Uuid BoundaryId,
    Uuid DraftVersionId, long ExpectedRevision, string Rationale)
    : IRequest, IBoundaryAuthoringRequest, IBoundaryProgramResourceRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
