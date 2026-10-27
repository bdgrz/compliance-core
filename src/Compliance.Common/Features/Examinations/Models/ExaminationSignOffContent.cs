using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>Exact source identities and acknowledged open items for management sign-off.</summary>
public sealed record ExaminationSignOffContent(
    Uuid ExaminationId,
    string ScopeKind,
    Uuid SourceSnapshotId,
    string SourceSnapshotSha256,
    Uuid PackageId,
    int PackageVersion,
    Uuid PackageManifestId,
    string PackageManifestSha256,
    Uuid AssertionId,
    int AssertionVersion,
    string AssertionContentSha256,
    Uuid RepresentationLetterId,
    string RepresentationLetterContentSha256,
    Uuid AuditOutcomeId,
    string AuditOutcomeContentSha256,
    IReadOnlyList<ExaminationContentReference> KnownExceptions,
    IReadOnlyList<Uuid> UnresolvedMaterialItemIds,
    IReadOnlyList<Uuid> AcknowledgedMaterialItemIds,
    string Decision,
    string Comments,
    IReadOnlyList<ManagementAssertionSigner> Approvers)
{
    public IReadOnlyList<ExaminationCommitment> Commitments { get; init; } = [];
}
