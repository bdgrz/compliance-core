using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>Assertion wording and the exact examination inputs management reviewed.</summary>
public sealed record ManagementAssertionContent(
    Uuid ExaminationId,
    string ScopeKind,
    Uuid SourceSnapshotId,
    string SourceSnapshotSha256,
    Uuid SystemDescriptionVersionId,
    string SystemDescriptionSha256,
    Uuid PackageId,
    int PackageVersion,
    Uuid PackageManifestId,
    string PackageManifestSha256,
    string WordingSource,
    Uuid? WordingReferenceId,
    string AssertionText,
    IReadOnlyList<ExaminationContentReference> SupportingInputs,
    IReadOnlyList<ManagementAssertionSigner> Signers);
