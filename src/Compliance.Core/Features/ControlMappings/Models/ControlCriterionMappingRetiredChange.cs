using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.ControlMappings.Models;

public sealed record ControlCriterionMappingRetiredChange(Uuid TenantId, Uuid ProgramId, Uuid MappingId, long Revision,
    int VersionNumber, string Rationale, Uuid ActorMemberId, string ActorDisplay, DateTimeOffset RetiredAt,
    Uuid ControlId, Uuid EditionId, string CriterionIdentifier, OperationProvenance Operation)
{
    [JsonIgnore]
    public ActorReference Actor => ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
