using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

public sealed record ControlCriterionMappingRetiredChange(Uuid TenantId, Uuid ProgramId, Uuid MappingId, long Revision,
    int VersionNumber, string Rationale, Uuid ActorMemberId, string ActorDisplay, DateTimeOffset RetiredAt,
    Uuid ControlId, Uuid EditionId, string CriterionIdentifier, OperationProvenance Operation)
{
    [JsonIgnore]
    public ActorReference Actor => ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
