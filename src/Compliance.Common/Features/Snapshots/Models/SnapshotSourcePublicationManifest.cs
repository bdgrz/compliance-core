using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Examinations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

/// <summary>Complete source-body publication proof, stored outside the bounded baseline fact and sealed by its digest.</summary>
public sealed record SnapshotSourcePublicationManifest(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId,
    IReadOnlyList<SnapshotSourcePublicationEntry> Sources)
{
    public const string Schema = "snapshot-source-publication-manifest.v1";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? RecordedThrough { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uuid? ClosePeriodId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uuid? CloseOperationId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<TypeIBaselineSourceCoverage>? SourceCoverage { get; init; }
}
