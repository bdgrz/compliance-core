namespace Bdgrz.Compliance.Features.Criteria.Models;

/// <summary>Supplied license permissions; absence of a permission never permits that use.</summary>
public sealed record CriteriaOverlayUsageFlags(bool Display, bool Export);
