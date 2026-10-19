using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;
using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the AdvisorFeedbackRecorded domain fact for an advisor handoff.</summary>
[Discriminator("bdgrz.advisor-feedback.recorded", 1)]
public sealed record AdvisorFeedbackRecorded(Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    Uuid FeedbackId, Uuid RecordId, Uuid RecordVersionId, string Kind, string Body,
    ActorReference Author, DateTimeOffset CreatedAt) : DomainEvent
{
    /// <summary>The native selected identity for sources whose version is not a UUID pair.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AdvisorRecordVersion? SelectedSource { get; init; }
}
