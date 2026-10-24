using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     A pending proposal on the mapping line of one (control, edition, criterion) tuple. The logical id of
///     <paramref name="Operation" /> is <paramref name="MappingId" />; <paramref name="Revision" /> counts the facts
///     of the tuple's line only.
/// </summary>
[Discriminator("bdgrz.control_mapping.proposed", 2)]
public sealed record ControlCriterionMappingProposed(Uuid TenantId, Uuid ProgramId, Uuid MappingId, long Revision,
    int VersionNumber, Uuid ControlId, Uuid ControlVersionId, Uuid EditionId, string CriterionIdentifier,
    string CriterionKind, string Rationale, string ApplicabilityExplanation, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset ProposedAt, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact
{
    /// <summary>The proposing member.</summary>
    [JsonIgnore]
    public ActorReference Actor => ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
