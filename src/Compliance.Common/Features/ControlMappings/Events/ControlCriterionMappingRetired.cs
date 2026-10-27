using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

[Discriminator("bdgrz.control_mapping.retired", 3)]
public sealed record ControlCriterionMappingRetired(Uuid TenantId, Uuid ProgramId, Uuid MappingId, long Revision,
    int VersionNumber, Uuid ActorMemberId, string ActorDisplay, DateTimeOffset RetiredAt,
    Uuid ControlId, Uuid EditionId, string CriterionIdentifier, ImmutableContentRef Content, MappingLineHeader Header, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, IMappingLineFact
{
    [JsonIgnore]
    public ActorReference Actor => ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
