using Bdgrz.Compliance.Features.Criteria.Models;
namespace Bdgrz.Compliance.Features.Criteria;

/// <summary>The bounds of supplied overlay content, unchanged from the product rule.</summary>
static class CriteriaTextOverlayRules
{
    public const int MaximumTextLength = 16000;

    /// <summary>The trimmed content, or null when any supplied part is missing, unbounded or has control characters.</summary>
    public static CriteriaTextOverlayContent? Clean(CriteriaTextOverlayContent? content)
    {
        if (content is null || string.IsNullOrWhiteSpace(content.Text) || content.Text.Length > MaximumTextLength ||
            content.Text.Any(static value => char.IsControl(value) && value is not ('\r' or '\n' or '\t')) ||
            string.IsNullOrWhiteSpace(content.Supplier) || content.Supplier.Length > 200 ||
            content.Supplier.Any(char.IsControl) || string.IsNullOrWhiteSpace(content.LicenseReference) ||
            content.LicenseReference.Length > 2000 || content.LicenseReference.Any(char.IsControl) ||
            content.UsageFlags is null)
            return null;
        return content with
        {
            Text = content.Text.Trim(),
            Supplier = content.Supplier.Trim(),
            LicenseReference = content.LicenseReference.Trim(),
        };
    }
}
