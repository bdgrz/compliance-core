using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.ControlMappings.Commands;

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
