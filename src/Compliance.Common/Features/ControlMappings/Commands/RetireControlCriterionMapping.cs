using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.ControlMappings.Commands;

/// <summary>
///     Withdraws the accepted mapping named by <paramref name="MappingId" /> from coverage while keeping its full
///     history. A mapping that a newer accepted successor superseded cannot be retired.
///     <paramref name="CommandId" /> is the logical operation identity.
/// </summary>
[Discriminator("bdgrz.control_mapping.retire", 2)]
public sealed record RetireControlCriterionMapping(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid MappingId,
    long ExpectedRevision, string Rationale)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
