using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports.Models;

/// <summary>An attributed export build attempt and its exact source reconciliation.</summary>
public sealed record ClientExportBuildAttemptView(
    Uuid AttemptId,
    int AttemptNumber,
    string Outcome,
    Uuid? ArchiveArtifactId,
    string? ArchiveSha256,
    string? FailureCode,
    Uuid? ManifestId,
    int IncludedRecordCount,
    int WithheldRecordCount,
    Uuid SourceSnapshotId,
    string SourceSnapshotSha256,
    Uuid? CompletedByMemberId,
    string CompletedByDisplay,
    DateTimeOffset CompletedAt,
    ActorReference? CompletedByActor = null)
{
    public ActorReference EffectiveCompletedBy => CompletedByActor ?? ActorReference.ForMember(
        CompletedByMemberId ?? Uuid.Empty, CompletedByDisplay);
}
