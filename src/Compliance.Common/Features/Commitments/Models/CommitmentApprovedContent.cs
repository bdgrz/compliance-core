using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Commitments.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
namespace Bdgrz.Compliance.Features.Commitments.Models;

/// <summary>A separate approval of an accepted review; it creates an immutable effective version.</summary>
public sealed record CommitmentApprovedContent(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long Revision, Uuid DecisionId, Uuid AcceptedReviewDecisionId, long Version,
    DateOnly EffectiveFrom, string ImpactDigest, string Rationale, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId = null) : ICommitmentMutationContent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
