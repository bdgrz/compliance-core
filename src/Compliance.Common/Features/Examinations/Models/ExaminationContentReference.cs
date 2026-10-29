using Cntryl.Portia;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>An immutable, exact source version included in an examination decision.</summary>
public sealed record ExaminationContentReference(
    string SourceType,
    Uuid RecordId,
    Uuid VersionId,
    long Version,
    string ContentSha256);
