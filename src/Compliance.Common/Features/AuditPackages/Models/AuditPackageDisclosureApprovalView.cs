using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AuditPackages.Models;

/// <summary>Exact-recipient approval for one package version and manifest.</summary>
public sealed record AuditPackageDisclosureApprovalView(
    Uuid DisclosureApprovalId,
    int PackageVersion,
    Uuid ManifestId,
    string ManifestSha256,
    string Recipient,
    string Purpose,
    string Rationale,
    Uuid ApprovedByMemberId,
    string ApprovedByDisplay,
    DateTimeOffset ApprovedAt);
