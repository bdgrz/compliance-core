using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.ControlMappings.Models;

public sealed record ControlCriterionMappingProposedChange(Uuid TenantId, Uuid ProgramId, Uuid MappingId, long Revision,
    int VersionNumber, Uuid ControlId, Uuid ControlVersionId, Uuid EditionId, string CriterionIdentifier,
    string CriterionKind, string Rationale, string ApplicabilityExplanation, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset ProposedAt, OperationProvenance Operation)
{
    [JsonIgnore]
    public ActorReference Actor => ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
