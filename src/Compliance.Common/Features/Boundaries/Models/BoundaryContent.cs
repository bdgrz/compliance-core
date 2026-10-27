namespace Bdgrz.Compliance.Features.Boundaries.Models;

public sealed record BoundaryContent(string Statement, string EngagementStage,
    IReadOnlyList<string> TrustServicesCategories, IReadOnlyList<BoundaryScopeEntry> Entries);
