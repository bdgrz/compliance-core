using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AuditPackages.Models;

/// <summary>An attributed rationale for accepting one explicitly overridable finding.</summary>
public sealed record AuditPackageValidationOverrideView(
    Uuid FindingId,
    string Rationale,
    Uuid ApprovedByMemberId,
    string ApprovedByDisplay,
    DateTimeOffset ApprovedAt);
