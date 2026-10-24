using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     The independent review of the pending proposal <paramref name="MappingId" />. The tuple fields name the
///     mapping line the fact belongs to; <paramref name="DecisionId" /> is the logical id of the review operation.
/// </summary>
[Discriminator("bdgrz.control_mapping.reviewed", 2)]
public sealed record ControlCriterionMappingReviewed(Uuid TenantId, Uuid ProgramId, Uuid MappingId, long Revision,
    int VersionNumber, Uuid DecisionId, string Outcome, string Rationale, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId, Uuid ControlId, Uuid EditionId,
    string CriterionIdentifier, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact
{
    /// <summary>The reviewing member.</summary>
    [JsonIgnore]
    public ActorReference Actor => ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
