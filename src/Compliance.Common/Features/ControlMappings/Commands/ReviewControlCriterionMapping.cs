using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     Accepts or rejects the pending proposal named by <paramref name="MappingId" /> at the exact revision of its
///     mapping line. <paramref name="CommandId" /> is the logical operation identity of this review.
/// </summary>
[Discriminator("bdgrz.control_mapping.review", 2)]
public sealed record ReviewControlCriterionMapping(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid MappingId,
    long ExpectedRevision, string Outcome, string Rationale, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
