using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Letter provenance, original artifact identity, and required executive signers.</summary>
public sealed record ManagementRepresentationLetterContent(
    Uuid ExaminationId,
    string ScopeKind,
    Uuid PackageId,
    int PackageVersion,
    Uuid PackageManifestId,
    string PackageManifestSha256,
    Uuid AssertionId,
    int AssertionVersion,
    string AssertionContentSha256,
    string SourceKind,
    string SourceReference,
    Uuid OriginalArtifactId,
    string OriginalArtifactSha256,
    DateOnly LetterDate,
    IReadOnlyList<ManagementAssertionSigner> RequiredSigners);
