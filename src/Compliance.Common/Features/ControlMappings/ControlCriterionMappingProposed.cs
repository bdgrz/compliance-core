using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

[Discriminator("bdgrz.control_mapping.proposed", 3)]
public sealed record ControlCriterionMappingProposed(Uuid TenantId, Uuid ProgramId, Uuid MappingId, long Revision,
    int VersionNumber, Uuid ControlId, Uuid ControlVersionId, Uuid EditionId, string CriterionIdentifier,
    string CriterionKind, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset ProposedAt, ImmutableContentRef Content, MappingLineHeader Header, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, IMappingLineFact
{
    [JsonIgnore]
    public ActorReference Actor => ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
