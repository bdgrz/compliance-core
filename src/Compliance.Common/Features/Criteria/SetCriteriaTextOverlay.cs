using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

/// <summary>
///     Creates or revises one tenant-supplied licensed criteria entry. <paramref name="CommandId" /> is the logical
///     operation identity, so an exact retry returns the original registration; <paramref name="ExpectedRevision" />
///     is the overlay's own revision and is 0 for the first set.
/// </summary>
[Discriminator("bdgrz.criteria.overlay.set", 2)]
public sealed record SetCriteriaTextOverlay(Uuid CommandId, Uuid TenantId, Uuid EditionId, string Identifier,
    long ExpectedRevision, CriteriaTextOverlayContent Content)
    : IRequest<CriteriaTextOverlayRegistration>, IProgramManagementRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
