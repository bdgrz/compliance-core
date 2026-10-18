using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>A gap retained by exact source record version for Type II planning.</summary>
public sealed record TypeIICarriedForwardGap(
    string SourceKind,
    Uuid SourceRecordId,
    Uuid SourceVersionId,
    long SourceVersion,
    string SourceSha256,
    string Disposition);
