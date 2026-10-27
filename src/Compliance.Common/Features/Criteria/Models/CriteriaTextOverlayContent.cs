namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>Tenant-supplied content and license assertions, not a product determination of rights.</summary>
public sealed record CriteriaTextOverlayContent(string Text, string Supplier, string LicenseReference,
    CriteriaOverlayUsageFlags UsageFlags);
