using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Immutable Type I baseline manifest and attributable amendment lineage.</summary>
public sealed record TypeIBaselineView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid ExaminationEngagementId,
    Uuid TypeIBaselineId,
    string Status,
    long Revision,
    IReadOnlyList<TypeIBaselineSourceReference> Sources,
    IReadOnlyList<string> ScopeDifferences,
    string CanonicalManifest,
    string ContentSha256,
    DateTimeOffset UpdatedAt,
    Uuid ActorMemberId,
    string ActorDisplay,
    string ConfirmationRationale,
    Uuid? AmendsBaselineId,
    string? AmendmentReason,
    IReadOnlyList<Uuid> ChangedRecordIds,
    IReadOnlyList<Uuid> AffectedPackageMaterialIds,
    int AmendmentDepth)
{
    public IReadOnlyList<TypeIBaselineSourceCoverage> SourceCoverage { get; init; } = [];
}
