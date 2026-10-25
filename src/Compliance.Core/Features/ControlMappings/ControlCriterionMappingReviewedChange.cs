using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

public sealed record ControlCriterionMappingReviewedChange(Uuid TenantId, Uuid ProgramId, Uuid MappingId, long Revision,
    int VersionNumber, Uuid DecisionId, string Outcome, string Rationale, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId, Uuid ControlId, Uuid EditionId,
    string CriterionIdentifier, OperationProvenance Operation)
{
    [JsonIgnore]
    public ActorReference Actor => ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
