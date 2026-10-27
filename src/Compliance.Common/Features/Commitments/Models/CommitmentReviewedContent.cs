using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Commitments.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Commitments.Events;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Commitments.Models;

/// <summary>
///     An independent review decision. Legacy accepted reviews carry a version; newer accepted
///     reviews verify source provenance and wait for a separate <see cref="CommitmentApproved"/>.
/// </summary>
public sealed record CommitmentReviewedContent(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long Revision, Uuid DecisionId, string Outcome, string? OwnerReference,
    string? Applicability, string? Interpretation, string? InterpretationNote,
    string Rationale, long? Version, DateOnly? EffectiveFrom, string? ImpactDigest,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset DecidedAt,
    Uuid? SeparationOfDutiesWaiverId = null, string? SourceVerification = null,
    string? SourceVerifiedReference = null, string? SourceEvidence = null) : ICommitmentMutationContent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
