using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.ControlMappings.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.ControlMappings.Events;

[Discriminator("bdgrz.control_mapping.retired", 3)]
public sealed record ControlCriterionMappingRetired(Uuid TenantId, Uuid ProgramId, Uuid MappingId, long Revision,
    int VersionNumber, Uuid ActorMemberId, string ActorDisplay, DateTimeOffset RetiredAt,
    Uuid ControlId, Uuid EditionId, string CriterionIdentifier, ImmutableContentRef Content, MappingLineHeader Header, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, IMappingLineFact
{
    [JsonIgnore]
    public ActorReference Actor => ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
