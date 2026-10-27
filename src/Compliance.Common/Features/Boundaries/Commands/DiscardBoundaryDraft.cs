using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
namespace Bdgrz.Compliance.Features.Boundaries.Commands;

[Discriminator("bdgrz.boundary.draft.discard", 1)]
public sealed record DiscardBoundaryDraft(Uuid CommandId, Uuid TenantId, Uuid BoundaryId,
    Uuid DraftVersionId, long ExpectedRevision, string Rationale)
    : IRequest, IBoundaryAuthoringRequest, IBoundaryProgramResourceRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
