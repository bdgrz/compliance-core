using Bdgrz.Compliance.Features.Remediation;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

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
