using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AuditPackages.Models;

/// <summary>Validation findings and their separately attributed, rationale-backed overrides.</summary>
public sealed record AuditPackageValidationView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid AuditPackageValidationId,
    Uuid AuditPackageId,
    int PackageVersion,
    Uuid ManifestId,
    string ManifestSha256,
    string ValidationKey,
    string Status,
    long Revision,
    DateTimeOffset UpdatedAt,
    Uuid ValidatedByMemberId,
    string ValidatedByDisplay,
    IReadOnlyList<AuditPackageValidationFinding> Findings,
    IReadOnlyList<AuditPackageValidationOverrideView> Overrides);
