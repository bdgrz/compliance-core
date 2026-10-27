using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages.Models;

/// <summary>A bounded validation finding tied to one immutable package manifest.</summary>
public sealed record AuditPackageValidationFinding(
    Uuid FindingId,
    string Code,
    string Severity,
    bool BlocksDelivery,
    bool OverrideAllowed,
    string Summary);
