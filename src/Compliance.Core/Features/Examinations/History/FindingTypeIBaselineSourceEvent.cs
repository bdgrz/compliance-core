using System.Text.Json;
using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
namespace Bdgrz.Compliance.Features.Examinations.History;

sealed record FindingTypeIBaselineSourceEvent(
    Uuid EventId,
    ulong ProgramStreamVersion,
    long FindingRevision,
    DateTimeOffset OccurredOn,
    string Kind,
    JsonElement Payload)
{
    [JsonIgnore]
    public IReadOnlyList<SnapshotSourceContentBlob>? ContentBlobs { get; init; }
}
