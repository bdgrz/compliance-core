using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

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
