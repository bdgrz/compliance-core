using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Evidence.Events;

/// <summary>Records the EvidenceSupportLinked domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.evidence.evidence-support-linked", 1)]
public sealed record EvidenceSupportLinked(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? OriginalExpectedRevision { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AuthoredIntentSha256 { get; init; }
    public Uuid ProgramId { get; init; }
    public Uuid ArtifactId { get; init; }
    public long ArtifactRevision { get; init; }
    public string? ArtifactContentSha256 { get; init; }
    public string? SupportedRecordType { get; init; }
    public Uuid SupportedRecordId { get; init; }
    public long SupportedRecordRevision { get; init; }
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public string? Purpose { get; init; }
    public string? SufficiencyRationale { get; init; }
    public string? ActorDisplay { get; init; }
}
