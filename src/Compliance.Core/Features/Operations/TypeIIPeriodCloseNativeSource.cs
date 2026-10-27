using Bdgrz.Compliance.Features.NativeOperations.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Snapshots;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Exact native payload bytes and their authoritative stream/event/version identity in a close source.</summary>
public sealed record TypeIIPeriodCloseNativeSource(NativeFactReference Fact, string EventKind, string PayloadSha256,
    JsonElement Payload)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<TypeIIPeriodCloseContentBlob>? ContentBlobs { get; init; }
    [JsonIgnore]
    internal IReadOnlyList<SnapshotSourceContentBlob>? PhysicalContentBlobs { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? RecordedAt { get; init; }
}
