using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>An immutable, attributable source boundary used by a population version.</summary>
public sealed record AuditPopulationSourceSnapshotView(
    Uuid SnapshotId,
    int Version,
    string SourceReference,
    string ContentSha256,
    int ExpectedTotal,
    bool CollectionComplete,
    string? CompletenessEvidence,
    DateTimeOffset CapturedAt,
    Uuid RecordedByMemberId,
    string RecordedByDisplay);
