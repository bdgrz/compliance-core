using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>One terminal named signature over exact assertion, package and baseline or approved close.</summary>
public sealed record ManagementAssertionSignatureView(int AssertionVersion, Uuid PackageId, int PackageVersion,
    Uuid PackageManifestId, string PackageManifestSha256, Uuid SourceSnapshotId, string SourceSnapshotSha256,
    string SourceSnapshotKind, long ExaminationRevision, Uuid SignerMemberId, string SignerRole,
    string SignerDisplay, DateTimeOffset SignedAt);
