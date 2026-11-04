using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;

namespace Bdgrz.Compliance.Features.Boundaries.Commands;

[Discriminator("bdgrz.boundary.draft.review", 1)]
public sealed record ReviewBoundaryDraft(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid DraftVersionId,
    long ExpectedRevision, string Outcome, string Rationale, DateOnly? EffectiveFrom = null, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest, IBoundaryAuthoringRequest, IProgramScopedRequest, IProgramLeadMutationRequest,
        IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
