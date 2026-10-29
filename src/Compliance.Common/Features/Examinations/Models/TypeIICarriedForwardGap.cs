using System.Text.Json.Serialization;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>A gap retained by exact source record version for Type II planning.</summary>
public sealed record TypeIICarriedForwardGap(
    string SourceKind,
    Uuid SourceRecordId,
    Uuid SourceVersionId,
    long SourceVersion,
    string SourceSha256,
    string Disposition,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Uuid? OwnerMemberId = null);
