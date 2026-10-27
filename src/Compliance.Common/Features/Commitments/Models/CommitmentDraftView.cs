using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Commitments.Models;

public sealed record CommitmentDraftView(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    Uuid ServiceId, string Kind, string Identifier, long Revision, string Status,
    string SourceResolution, string OwnerResolution, string ApplicabilityResolution,
    string Statement, string Context, string SourceReference,
    Uuid LastChangedByMemberId, string LastChangedByDisplay, DateTimeOffset LastChangedAt)
{
    /// <summary>The subservice provider responsible for this CSOC, when recorded.</summary>
    public Uuid? ProviderId { get; init; }

    readonly ActorReference? _lastChangedBy;

    /// <summary>The snapshotted actor; pre-snapshot rows fall back to the member columns.</summary>
    [JsonPropertyName("last_changed_by")]
    public ActorReference LastChangedBy
    {
        get => _lastChangedBy ?? ActorReference.ForMember(LastChangedByMemberId,
            LastChangedByDisplay);
        init => _lastChangedBy = value;
    }
}
