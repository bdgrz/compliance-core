using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

sealed record RiskSourceEvent(string Area, Uuid AggregateId, Uuid EventId,
    ulong AggregateVersion, DateTimeOffset RecordedAt, string Kind, JsonElement Content)
{
    // Physical source publication retains these bytes without changing the historical logical digest preimage.
    [JsonIgnore]
    public IReadOnlyList<SnapshotSourceContentBlob>? ContentBlobs { get; init; }
}
