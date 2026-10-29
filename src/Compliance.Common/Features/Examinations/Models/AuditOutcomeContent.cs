using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>Auditor-supplied report result bound to the exact examined package and snapshot.</summary>
public sealed record AuditOutcomeContent(
    Uuid ExaminationId,
    string ScopeKind,
    Uuid SourceSnapshotId,
    string SourceSnapshotSha256,
    Uuid PackageId,
    int PackageVersion,
    Uuid PackageManifestId,
    string PackageManifestSha256,
    string SourceKind,
    string OutcomeCode,
    string OutcomeSummary,
    string ReportReference,
    Uuid? ReportArtifactId,
    string? ReportArtifactSha256,
    DateOnly IssuedOn);
