using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     The retirement of the accepted mapping <paramref name="MappingId" /> from coverage. The tuple fields name the
///     mapping line the fact belongs to.
/// </summary>
[Discriminator("bdgrz.control_mapping.retired", 2)]
public sealed record ControlCriterionMappingRetired(Uuid TenantId, Uuid ProgramId, Uuid MappingId, long Revision,
    int VersionNumber, string Rationale, Uuid ActorMemberId, string ActorDisplay, DateTimeOffset RetiredAt,
    Uuid ControlId, Uuid EditionId, string CriterionIdentifier, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact
{
    /// <summary>The retiring member.</summary>
    [JsonIgnore]
    public ActorReference Actor => ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
