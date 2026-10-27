using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Snapshots.Models;
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>Identifies one exact immutable source used by a Type I baseline.</summary>
public sealed record TypeIBaselineSourceReference(
    string Kind,
    Uuid RecordId,
    Uuid VersionId,
    long Revision,
    string ContentSha256,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SnapshotSourceBlob? Content = null);
