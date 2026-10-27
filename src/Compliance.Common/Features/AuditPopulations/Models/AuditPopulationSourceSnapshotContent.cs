using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AuditPopulations.Models;

/// <summary>A pinned source export or documented manual collection boundary.</summary>
public sealed record AuditPopulationSourceSnapshotContent(
    Uuid SnapshotId,
    int Version,
    string SourceReference,
    string ContentSha256,
    int ExpectedTotal,
    bool CollectionComplete,
    string? CompletenessEvidence,
    DateTimeOffset CapturedAt);
