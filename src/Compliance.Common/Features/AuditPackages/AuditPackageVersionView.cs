using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>One immutable package selection and its build, validation, disclosure, and delivery history.</summary>
public sealed record AuditPackageVersionView(
    int PackageVersion,
    string Status,
    Uuid SourceSnapshotId,
    string SourceSnapshotKind,
    string SourceSnapshotSha256,
    IReadOnlyList<AuditPackageSelectionEntry> SelectedContent,
    AuditPackageManifestView Manifest,
    IReadOnlyList<AuditPackageBuildAttemptView> BuildAttempts,
    IReadOnlyList<AuditPackageValidationView> Validations,
    IReadOnlyList<AuditPackageDisclosureApprovalView> DisclosureApprovals,
    IReadOnlyList<AuditPackageDeliveryView> Deliveries,
    int? AmendsVersion,
    string? AmendmentReason,
    Uuid CreatedByMemberId,
    string CreatedByDisplay,
    DateTimeOffset CreatedAt);
