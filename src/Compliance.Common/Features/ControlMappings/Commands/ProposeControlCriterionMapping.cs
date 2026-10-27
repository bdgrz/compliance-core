using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.ControlMappings.Commands;

/// <summary>
///     Proposes that an exact approved control version addresses one criterion or point of focus in the program's
///     selected edition. <paramref name="MappingId" /> identifies this immutable proposal and is the logical operation
///     identity, so an exact retry returns the original registration even after the proposal was reviewed.
///     <paramref name="ExpectedRevision" /> is the revision of the (control, edition, criterion) tuple's mapping line:
///     0 for the first proposal and the current revision for a successor.
/// </summary>
[Discriminator("bdgrz.control_mapping.propose", 2)]
public sealed record ProposeControlCriterionMapping(Uuid MappingId, Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid ControlVersionId, Uuid EditionId, string CriterionIdentifier, long ExpectedRevision, string Rationale,
    string ApplicabilityExplanation)
    : IRequest<ControlCriterionMappingRegistration>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => MappingId;
}
