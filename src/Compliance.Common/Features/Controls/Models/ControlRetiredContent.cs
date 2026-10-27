using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Controls.Contracts;
namespace Bdgrz.Compliance.Features.Controls.Models;

/// <summary>
///     Ends future use of one exact approved control version. Prior versions, decisions, and
///     relationships are retained and stay readable.
/// </summary>
public sealed record ControlRetiredContent(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid RetirementId, Uuid VersionId, long Revision, Uuid DecisionId,
    Uuid AcceptedReviewDecisionId, DateOnly EffectiveUntil, string ImpactDigest,
    Uuid ActorMemberId, string ActorDisplay, string Rationale, DateTimeOffset DecidedAt,
    Uuid? SeparationOfDutiesWaiverId = null) : IControlMutationContent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId,
        ActorDisplay);
}
