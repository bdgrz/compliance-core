using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings.Events;

[Discriminator("bdgrz.control_mapping.reviewed", 3)]
public sealed record ControlCriterionMappingReviewed(Uuid TenantId, Uuid ProgramId, Uuid MappingId, long Revision,
    int VersionNumber, Uuid DecisionId, string Outcome, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId, Uuid ControlId, Uuid EditionId,
    string CriterionIdentifier, ImmutableContentRef Content, MappingLineHeader Header, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, IMappingLineFact
{
    [JsonIgnore]
    public ActorReference Actor => ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
