using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Commitments.Events;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments.Models;

/// <summary>Records the CommitmentRetired domain fact for one tenant-owned subject revision.</summary>
public sealed record CommitmentRetiredContent(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    string ActorDisplay,
    DateOnly EffectiveOn,
    string Rationale,
    DateTimeOffset OccurredAt) : ICommitmentMutationContent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId ?? Uuid.Empty,
        ActorDisplay);
}
