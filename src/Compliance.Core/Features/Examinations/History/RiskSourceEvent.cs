using System.Text.Json;
using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.Examinations.History;

sealed record RiskSourceEvent(string Area, Uuid AggregateId, Uuid EventId,
    ulong AggregateVersion, DateTimeOffset RecordedAt, string Kind, JsonElement Content)
{
    // Physical source publication retains these bytes without changing the historical logical digest preimage.
    [JsonIgnore]
    public IReadOnlyList<SnapshotSourceContentBlob>? ContentBlobs { get; init; }
}
